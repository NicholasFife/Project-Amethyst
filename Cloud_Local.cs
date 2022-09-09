using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class Cloud_Local : MonoBehaviour
{
    #region "State Machine"
    public enum CloudStates
    {
        Active, //Default state
        Shrinking, //In vision radius - includes clouds that have scaled to 0,0,0
        Growing, //Transition state from shrinking to active
    }

    CloudStates curState = CloudStates.Active;
    Dictionary<CloudStates, Action> csm = new Dictionary<CloudStates, Action>();
    #endregion

    Renderer myRend; //Renderer reference used to turn cloud translucent
    public Material mDefault;
    public Material mTranslucent;
    bool isTranslucent;
    Vector3 myScale = new Vector3 (1,1,1);

    float scaleSpeed = 5; //Adjusts the speed at which this cloud shrinks/grows
    float maxSize = 1f; //Maximum size of cloud

    Cloud_Mgr myMgr;

    public int myArrayOffset; //This specific cloud's offset (as measured in the WorldCloud array) from the centermost Local Cloud's location
    public int myCurWorldCloud = 0; //Stored value of the WorldCloud array location this cloud currently represents

    public LayerMask obstacles; //Obstacles that block player's vision

    bool wasChecked = false; // sets to true after each cascade check and resets to false before each new cascade check
    int maxSteps = 12; //Maximum horizontal and vertical steps from the player cloud that can be checked. 
    int maxDistance = 8; //Maximum total distance from player cloud to be valid for checking
    public bool inVisionRange = false;

    private void Awake()
    {
        SetWasChecked(false); //Cloud_Mgr already initialized a check and this is true by the time Awake is called on this script
    }

    //Called from Cloud_Mgr before Awake has been run on this specific script so Cloud_Mgr can run all the checks it needs when it runs Awake
    public void Initialize(Cloud_Mgr m)
    {
        myMgr = m;
        myRend = GetComponent<Renderer>();

        csm.Add(CloudStates.Active, new Action(StateActive));
        csm.Add(CloudStates.Shrinking, new Action(StateShrinking));
        csm.Add(CloudStates.Growing, new Action(StateGrowing));
    }
    //set the numerical representation of the number of array places this cloud is offset from the centermost localCloud
    public void SetArrayOffset(int i)
    {
        myArrayOffset = i;
    }
    //retrieve the numerical representation of the number of array places this cloud is offset from the centermost localCloud
    public int GetArrayOffset()
    {
        return myArrayOffset;
    }

    public int GetMyCloud()
    {
        return myCurWorldCloud;
    }
    public void SetMyCloud(int i)
    {
        myCurWorldCloud = i;
        myMgr.thisWorldCloud[i].SetMyLocalCloud(this);
        UpdateMyStatus();
    }
    //Is this cloud valid to check during a cascade check? (Cascade checks look to see if it should enter shrinking)
    public bool GetInVisionRange()
    { return inVisionRange; }
    public void SetInVisionRange(bool b)
    { inVisionRange = b; }

    //Update the worldCloud data withe the scale of this object
    public void UpdateWorldCloud()
    {
        myMgr.thisWorldCloud[myCurWorldCloud].DefineThisCloud(transform.localScale, CloudStates.Growing);
    }
    //Change this localCloud to reflect the worldCloud it is set to simulate
    void UpdateMyStatus()
    {
        if (myMgr != null)
        { 
            myScale = myMgr.thisWorldCloud[myCurWorldCloud].GetMyScale();
            if (transform.localScale != myScale)
            { transform.localScale = myScale; }
            curState = myMgr.thisWorldCloud[myCurWorldCloud].GetMyState();
             
            if (myMgr.thisWorldCloud[myCurWorldCloud].GetIsTranslucent() == true && isTranslucent == false)
            { 
                myRend.material = mTranslucent;
                isTranslucent = true;
            }
            else if (myMgr.thisWorldCloud[myCurWorldCloud].GetIsTranslucent() == false && isTranslucent == true)
            { 
                myRend.material = mDefault;
                isTranslucent = false;
            }
        }
    }

    //This Update runs after Cloud_Mgr.Update, 
    void Update()
    {
        SetWasChecked(false); //prepare for the next CascadeCheck
        csm[curState].Invoke(); //Invoke the current state to shrink/grow/do nothing
    }

    public CloudStates GetCurState()
    { return curState; }

    void EnterActive()
    { curState = CloudStates.Active; }
    void StateActive()
    {  }

    public void EnterShrinking()
    { curState = CloudStates.Shrinking; }
    void StateShrinking()
    {
        //Needs to chrink
        if (myScale.x >= .01)
        {
            myScale = new Vector3(myScale.x - Time.deltaTime * scaleSpeed, myScale.y - Time.deltaTime * scaleSpeed, myScale.z - Time.deltaTime * scaleSpeed);
            transform.localScale = myScale;
        }
        //Shrank enough
        if (myScale.x <= .01)
        {
            //Can no longer be seen until it grows again
            transform.localScale = new Vector3(0, 0, 0);

            //Change to translucent
            if (myMgr.thisWorldCloud[myCurWorldCloud].GetIsTranslucent() == false)
            {
                myRend.material = mTranslucent;
                isTranslucent = true;
                myMgr.thisWorldCloud[myCurWorldCloud].SetTranslucent(true);
            }
        }

        //EnterGrowing();
    }
    void EnterGrowing()
    { curState = CloudStates.Growing; }

    void StateGrowing()
    {
        //Grow
        if (myScale.x < maxSize)
        {
            myScale = new Vector3(myScale.x + Time.deltaTime * scaleSpeed, myScale.y + Time.deltaTime * scaleSpeed, myScale.z + Time.deltaTime * scaleSpeed);
            transform.localScale = myScale;
        }
        //Grown big enough, return to Active State
        if (myScale.x >= maxSize)
        {
            myScale = new Vector3(maxSize, maxSize, maxSize);
            transform.localScale = myScale;

            EnterActive();
        }
    }
    //Get or Set if Cascade check already ran on this cloud
    public bool GetWasChecked()
    { return wasChecked; } 
    public void SetWasChecked(bool b)
    {
        //Debug.Log("SetWasChecked(" + b + ")");
        wasChecked = b; 
    }

    #region "CascadeInitialization"
    //Runs when Cloud_Mgr calls Awake
    //Query neighbors according to given direction. 
    //First call dir 4 begins checks in all directions.
    //Then dir 0 "North" = west, north, east
    //dir 1 "West" = north, west, south
    //dir 2 "East" = north, east, south
    //dir 3 "South" = west, south, east
    public void InitializeCascadeChecks(int dir, int steps, Vector3 pCloud)
    {
        if (dir == 0)
        { InitializeNChecks(steps, pCloud); }
        if (dir == 1)
        { InitializeWChecks(steps,  pCloud); }
        if (dir == 2)
        { InitializeEChecks(steps, pCloud); }
        if (dir == 3)
        { InitializeSChecks(steps, pCloud); }
        if (dir == 4)
        {
            EnterShrinking();
            InitializeAllChecks(steps, pCloud); 
        }
    }

    //Tells neighbors to check if they are valid according to the given direction.
    //If they are valid, they will then do the same with their neighbors and so on until all valid locations have been checked
    void InitializeNChecks(int steps, Vector3 pCloud)
    {
        if (myMgr.thisWorldCloud[myCurWorldCloud].GetANeighbor(0) != null)
        { InitializeNeighbor(myMgr.thisWorldCloud[myCurWorldCloud].GetANeighbor(0).GetMyLocalCloud(), steps, pCloud, 0); }
        if (myMgr.thisWorldCloud[myCurWorldCloud].GetANeighbor(1) != null)
        { InitializeNeighbor(myMgr.thisWorldCloud[myCurWorldCloud].GetANeighbor(1).GetMyLocalCloud(), steps, pCloud, 0); }
        if (myMgr.thisWorldCloud[myCurWorldCloud].GetANeighbor(2) != null)
        { InitializeNeighbor(myMgr.thisWorldCloud[myCurWorldCloud].GetANeighbor(2).GetMyLocalCloud(), steps, pCloud, 0); }
    }
    void InitializeWChecks(int steps, Vector3 pCloud)
    {
        if (myMgr.thisWorldCloud[myCurWorldCloud].GetANeighbor(0) != null)
        { InitializeNeighbor(myMgr.thisWorldCloud[myCurWorldCloud].GetANeighbor(0).GetMyLocalCloud(), steps, pCloud, 1); }
        if (myMgr.thisWorldCloud[myCurWorldCloud].GetANeighbor(1) != null)
        { InitializeNeighbor(myMgr.thisWorldCloud[myCurWorldCloud].GetANeighbor(1).GetMyLocalCloud(), steps, pCloud, 1); }
        if (myMgr.thisWorldCloud[myCurWorldCloud].GetANeighbor(3) != null)
        { InitializeNeighbor(myMgr.thisWorldCloud[myCurWorldCloud].GetANeighbor(3).GetMyLocalCloud(), steps, pCloud, 1); }
    }
    void InitializeEChecks(int steps, Vector3 pCloud)
    {
        if (myMgr.thisWorldCloud[myCurWorldCloud].GetANeighbor(0) != null)
        { InitializeNeighbor(myMgr.thisWorldCloud[myCurWorldCloud].GetANeighbor(0).GetMyLocalCloud(), steps, pCloud, 2); }
        if (myMgr.thisWorldCloud[myCurWorldCloud].GetANeighbor(2) != null)
        { InitializeNeighbor(myMgr.thisWorldCloud[myCurWorldCloud].GetANeighbor(2).GetMyLocalCloud(), steps, pCloud, 2); }
        if (myMgr.thisWorldCloud[myCurWorldCloud].GetANeighbor(3) != null)
        { InitializeNeighbor(myMgr.thisWorldCloud[myCurWorldCloud].GetANeighbor(3).GetMyLocalCloud(), steps, pCloud, 2); }
    }
    void InitializeSChecks(int steps, Vector3 pCloud)
    {
        if (myMgr.thisWorldCloud[myCurWorldCloud].GetANeighbor(1) != null)
        { InitializeNeighbor(myMgr.thisWorldCloud[myCurWorldCloud].GetANeighbor(1).GetMyLocalCloud(), steps, pCloud, 3); }
        if (myMgr.thisWorldCloud[myCurWorldCloud].GetANeighbor(2) != null)
        { InitializeNeighbor(myMgr.thisWorldCloud[myCurWorldCloud].GetANeighbor(2).GetMyLocalCloud(), steps, pCloud, 3); }
        if (myMgr.thisWorldCloud[myCurWorldCloud].GetANeighbor(3) != null)
        { InitializeNeighbor(myMgr.thisWorldCloud[myCurWorldCloud].GetANeighbor(3).GetMyLocalCloud(), steps, pCloud, 3); }
    }
    void InitializeAllChecks(int steps, Vector3 pCloud)
    {
        if (myMgr.thisWorldCloud[myCurWorldCloud].GetANeighbor(0) != null)
        { InitializeNeighbor(myMgr.thisWorldCloud[myCurWorldCloud].GetANeighbor(0).GetMyLocalCloud(), steps, pCloud, 0); }
        if (myMgr.thisWorldCloud[myCurWorldCloud].GetANeighbor(1) != null)
        { InitializeNeighbor(myMgr.thisWorldCloud[myCurWorldCloud].GetANeighbor(1).GetMyLocalCloud(), steps, pCloud, 1); }
        if (myMgr.thisWorldCloud[myCurWorldCloud].GetANeighbor(2) != null)
        { InitializeNeighbor(myMgr.thisWorldCloud[myCurWorldCloud].GetANeighbor(2).GetMyLocalCloud(), steps, pCloud, 2); }
        if (myMgr.thisWorldCloud[myCurWorldCloud].GetANeighbor(3) != null)
        { InitializeNeighbor(myMgr.thisWorldCloud[myCurWorldCloud].GetANeighbor(3).GetMyLocalCloud(), steps, pCloud, 3); }
    }

    //Runs the check on a given neighbor and initiates further action if needed
    void InitializeNeighbor(Cloud_Local c, int steps, Vector3 pCloud, int dir)
    { 
        //Prevent double checking the neighboring cloud
        if (c.GetWasChecked() == false)
        {
            //Mark as checked
            c.SetWasChecked(true);

            //Validate the neighbor, Set bool if true, and check its neighbors
            if (dir == 0 && NorthValidation(c, steps, pCloud) == true)
            {
               //Sets as within maximum range of vision
               c.SetInVisionRange(true);
               //Checks if cloud should be revealed immediately
               c.RevealOnInitialize(c, pCloud);
               //Continues Cascade Checks to the north
               c.InitializeCascadeChecks(0, steps + 1, pCloud); 
            }
            //Same as above, except for west, east, and south
            else if (dir == 1 && WestValidation(c, steps, pCloud) == true)
            {
                c.SetInVisionRange(true);
                c.RevealOnInitialize(c, pCloud);
                c.InitializeCascadeChecks(1, steps + 1, pCloud);
            }
            else if (dir == 2 && EastValidation(c, steps, pCloud) == true)
            {
                c.SetInVisionRange(true);
                c.RevealOnInitialize(c, pCloud);
                c.InitializeCascadeChecks(2, steps + 1, pCloud);
            }
            else if (dir == 3 && SouthValidation(c, steps, pCloud) == true)
            {
                c.SetInVisionRange(true);
                c.RevealOnInitialize(c, pCloud);
                c.InitializeCascadeChecks(3, steps + 1, pCloud);
            }
        }
    }
    //Checks for line of sight and sets cloud state to shrink if appropriate
    void RevealOnInitialize(Cloud_Local c, Vector3 pCloud)
    {
        bool hasLOS = !Physics.Linecast(c.transform.position, pCloud, obstacles);

        //linecast does not detect an obstacle - object needs scaled down if not already
        if (hasLOS)
        { c.EnterShrinking(); }
        else
        { c.EnterGrowing(); }
    } 
    #endregion

    #region "Validation"
    //Validation checks confirm that a given cloud is within the following parameters
    //North-South distance and East-West distance is not greater than maximum distance
    //Cloud is in appropriate cardinal direction
    //Steps taken to reach this cloud were not greater than maximum steps
    //Cloud is in the correct direction from cascade's starting point
    bool NorthValidation(Cloud_Local c, int steps, Vector3 pCloud)
    {
        float distNS = c.transform.position.z - pCloud.z;
        float distEW = c.transform.position.x - pCloud.x;
        if (steps <= maxSteps && (distNS <= maxDistance && ((distEW >= -maxDistance && distEW <= 0) || (distEW <= maxDistance && distEW >= 0))))
        { return true; }
        else
        { return false; }
    }
    bool WestValidation(Cloud_Local c, int steps, Vector3 pCloud)
    {
        float distNS = c.transform.position.z - pCloud.z;
        float distEW = c.transform.position.x - pCloud.x;

        if (steps <= maxSteps && (distEW >= -maxDistance && ((distNS <= 0 && distNS >= -maxDistance) || (distNS >= 0 && distNS <= maxDistance))))
        { return true; }
        else
        { return false; }
    }
    bool EastValidation(Cloud_Local c, int steps, Vector3 pCloud)
    {
        float distNS = c.transform.position.z - pCloud.z;
        float distEW = c.transform.position.x - pCloud.x;

        if (steps <= maxSteps && (distEW <= maxDistance && ((distNS <= 0 && distNS >= -maxDistance) || (distNS >= 0 && distNS <= maxDistance))))
        { return true; }
        else
        { return false; }
    }
    bool SouthValidation(Cloud_Local c, int steps, Vector3 pCloud)
    {
        float distNS = c.transform.position.z - pCloud.z;
        float distEW = c.transform.position.x - pCloud.x;

        if (steps <= maxSteps && (distNS >= -maxDistance && ((distEW >= -maxDistance && distEW <= 0) || (distEW <= maxDistance && distEW >= 0))))
        { return true; }
        else
        { return false; }
    }
    #endregion
     
    //Called to start next wave of checks from a cloud
    public void CascadeChecks(int dir, int steps, Vector3 pCloud)
    {
        if (dir == 0)
        { NChecks(steps, pCloud); }
        if (dir == 1)
        { WChecks(steps, pCloud); }
        if (dir == 2)
        { EChecks(steps, pCloud); }
        if (dir == 3)
        { SChecks(steps, pCloud); }
        if (dir == 4)
        { AllChecks(steps, pCloud); }
    }

    //used to trigger appropriate checks based on direction of search
    void NChecks(int steps, Vector3 pCloud)
    {
        if (myMgr.thisWorldCloud[myCurWorldCloud].GetANeighbor(0) != null)
        { CheckNeighbor(myMgr.thisWorldCloud[myCurWorldCloud].GetANeighbor(0).GetMyLocalCloud(), steps, pCloud, 0) ; }
        if (myMgr.thisWorldCloud[myCurWorldCloud].GetANeighbor(1) != null)
        { CheckNeighbor(myMgr.thisWorldCloud[myCurWorldCloud].GetANeighbor(1).GetMyLocalCloud(), steps, pCloud, 0); }
        if (myMgr.thisWorldCloud[myCurWorldCloud].GetANeighbor(2) != null)
        { CheckNeighbor(myMgr.thisWorldCloud[myCurWorldCloud].GetANeighbor(2).GetMyLocalCloud(), steps, pCloud, 0); }
    }
    void WChecks(int steps, Vector3 pCloud)
    {
        if (myMgr.thisWorldCloud[myCurWorldCloud].GetANeighbor(0) != null)
        { CheckNeighbor(myMgr.thisWorldCloud[myCurWorldCloud].GetANeighbor(0).GetMyLocalCloud(), steps, pCloud, 1); }
        if (myMgr.thisWorldCloud[myCurWorldCloud].GetANeighbor(1) != null)
        { CheckNeighbor(myMgr.thisWorldCloud[myCurWorldCloud].GetANeighbor(1).GetMyLocalCloud(), steps, pCloud, 1); }
        if (myMgr.thisWorldCloud[myCurWorldCloud].GetANeighbor(3) != null)
        { CheckNeighbor(myMgr.thisWorldCloud[myCurWorldCloud].GetANeighbor(3).GetMyLocalCloud(), steps, pCloud, 1); }
    }
    void EChecks(int steps, Vector3 pCloud)
    {
        if (myMgr.thisWorldCloud[myCurWorldCloud].GetANeighbor(0) != null)
        { CheckNeighbor(myMgr.thisWorldCloud[myCurWorldCloud].GetANeighbor(0).GetMyLocalCloud(), steps, pCloud, 2); }
        if (myMgr.thisWorldCloud[myCurWorldCloud].GetANeighbor(2) != null)
        { CheckNeighbor(myMgr.thisWorldCloud[myCurWorldCloud].GetANeighbor(2).GetMyLocalCloud(), steps, pCloud, 2); }
        if (myMgr.thisWorldCloud[myCurWorldCloud].GetANeighbor(3) != null)
        { CheckNeighbor(myMgr.thisWorldCloud[myCurWorldCloud].GetANeighbor(3).GetMyLocalCloud(), steps, pCloud, 2); }
    }
    void SChecks(int steps, Vector3 pCloud)
    {
        if (myMgr.thisWorldCloud[myCurWorldCloud].GetANeighbor(1) != null)
        { CheckNeighbor(myMgr.thisWorldCloud[myCurWorldCloud].GetANeighbor(1).GetMyLocalCloud(), steps, pCloud, 3); }
        if (myMgr.thisWorldCloud[myCurWorldCloud].GetANeighbor(2) != null)
        { CheckNeighbor(myMgr.thisWorldCloud[myCurWorldCloud].GetANeighbor(2).GetMyLocalCloud(), steps, pCloud, 3); }
        if (myMgr.thisWorldCloud[myCurWorldCloud].GetANeighbor(3) != null)
        { CheckNeighbor(myMgr.thisWorldCloud[myCurWorldCloud].GetANeighbor(3).GetMyLocalCloud(), steps, pCloud, 3); }
    }
    void AllChecks(int steps, Vector3 pCloud)
    {
        if (myMgr.thisWorldCloud[myCurWorldCloud].GetANeighbor(0) != null)
        { CheckNeighbor(myMgr.thisWorldCloud[myCurWorldCloud].GetANeighbor(0).GetMyLocalCloud(), steps, pCloud, 0); }
        if (myMgr.thisWorldCloud[myCurWorldCloud].GetANeighbor(1) != null)
        { CheckNeighbor(myMgr.thisWorldCloud[myCurWorldCloud].GetANeighbor(1).GetMyLocalCloud(), steps, pCloud, 1); }
        if (myMgr.thisWorldCloud[myCurWorldCloud].GetANeighbor(2) != null)
        { CheckNeighbor(myMgr.thisWorldCloud[myCurWorldCloud].GetANeighbor(2).GetMyLocalCloud(), steps, pCloud, 2); }
        if (myMgr.thisWorldCloud[myCurWorldCloud].GetANeighbor(3) != null)
        { CheckNeighbor(myMgr.thisWorldCloud[myCurWorldCloud].GetANeighbor(3).GetMyLocalCloud(), steps, pCloud, 3); }
    }

    //The actual check that needs run on individual cloud 
    void CheckNeighbor(Cloud_Local c, int steps, Vector3 pCloud, int dir)
    { 
        if (c.GetWasChecked() == false)
        {
            c.SetWasChecked(true);

            if (dir == 0 && c.GetInVisionRange() == true)
            { CheckReveal(c, pCloud, steps, 0); }
            else if (dir == 1 && c.GetInVisionRange() == true)
            { CheckReveal(c, pCloud, steps, 1); }
            else if (dir == 2 && c.GetInVisionRange() == true)
            { CheckReveal(c, pCloud, steps, 2); }
            else if (dir == 3 && c.GetInVisionRange() == true)
            { CheckReveal(c, pCloud, steps, 3); }
        }
    }
     
    //PRIMARY - Used with Cascade Checks to see if target cloud is within LOS
    void CheckReveal(Cloud_Local c, Vector3 pCloud, int steps, int dir)
    {
        bool hasLOS = !Physics.Linecast(c.transform.position, pCloud, obstacles);

        //linecast does not detect an obstacle - object needs scaled down if not already
        if (hasLOS)
        {
            c.CascadeChecks(dir, steps + 1, pCloud);

            c.EnterShrinking();
        }
        else
        { c.EnterGrowing(); }
    }
}
