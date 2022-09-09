using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Cloud_Mgr : MonoBehaviour
{
    //Generation
    public GameObject oneCloud; //prefab of a physical cloud
    [SerializeField]
    int mapWidth = 200; //Total width of the virtual cloud network
    [SerializeField]
    int mapHeight = 200 ;//Total height of the virtual cloud network
  
    int cloudWidth = 41; //Width of the local cloud network
    int cloudHeight = 25; //Height of the local cloud network

    //Dual Arrays working together to simulate a full-map fog of war
    //worldCloud is an array of simulated clouds storing data. 
    //localClouds is an array of actual gameObjects that pull data from the worldCloud
    //without having to have actual gameObjects for every entry in the worldCloud
 
    //localClouds - references to an array of scripts attached to game objects surrounding the player's field of vision
    public Cloud_Local[] localClouds;
    //worldClouds - used by localClouds to inform them of what scale and state they should be in
    public class worldCloud
    {
        Vector3 myScale = new Vector3(1,1,1);
        Cloud_Local.CloudStates myState = Cloud_Local.CloudStates.Active;
        //Array storing the neighboring clouds 0=North, 1=West, 2=East, 3=South
        worldCloud[] neighbors = new worldCloud[4]; 
        bool isTranslucent = false;
        Cloud_Local myLocalCloud = null; //Holds reference to the LocalCloud currently representing this worldCloud

        //Used to update stored worldCloud data
        public void DefineThisCloud(Vector3 scale, Cloud_Local.CloudStates state)
        {
            myScale = scale;
            myState = state;
        }
        public Vector3 GetMyScale()
        {
            return myScale;
        }
        public Cloud_Local.CloudStates GetMyState()
        {
            return myState;
        }
        public bool GetIsTranslucent()
        { return isTranslucent; }
        public void SetANeighbor(int i, worldCloud w)
        {
            neighbors[i] = w;
        }
        public worldCloud GetANeighbor(int i)
        { return neighbors[i]; }
        public void SetTranslucent(bool b)
        {
            isTranslucent = b;
        }
        public void SetMyLocalCloud (Cloud_Local l)
        {
            myLocalCloud = l;
        }
        public Cloud_Local GetMyLocalCloud ()
        {
            return myLocalCloud;
        }
    }

    public worldCloud[] thisWorldCloud;

    Transform pTrans; //Player Transform
    public int curX; //Player's current X position
    public int curZ; //Player's current Z position
    public int curCloud; //WorldCloud at player's current location
    
    void Awake()
    {
        //Assign References
        pTrans = FindObjectOfType<Player_Entity>().transform;

        //Create the visible clouds
        GenerateClouds();
        //Initialize thisWorldCloud[] data and set neighbors[] for each
        InitializeCloudData();
        //Initialize starting data for localCloud[]
        InitializeLocalClouds();

        //Update CloudManagerPosition to player
        FirstCenterClouds();

        //Do Preliminary checks to mark localClouds as within range to check
        thisWorldCloud[curCloud].GetMyLocalCloud().InitializeCascadeChecks(4, 0 + 1, new Vector3(transform.position.x, transform.position.y, transform.position.z));

    }

    #region "Generate Arrays and Initialize Data"
    //Used to generate the cloud game objects that fill the player's screen
    void GenerateClouds()
    {
        int offsetHeight = (cloudHeight - 1) / 2;
        int offsetWidth = (cloudWidth - 1) / 2;

        //Create the local cloud array in specified order, name them, set this as their parent, and initialize them
        for (int x = -offsetWidth; x <= offsetWidth; x++)
        {
            for (int y = -offsetHeight; y <= offsetHeight; y++)
            {
                GameObject go = Instantiate(oneCloud, new Vector3(transform.position.x + x, transform.position.y+.5f, transform.position.z + y), Quaternion.identity);

                go.name = "Cloud_" + x + "_" + y;
                go.transform.SetParent(transform);
                go.GetComponent<Cloud_Local>().Initialize(this);
            }
        }
        //Build the localCloud array
        localClouds = GetComponentsInChildren<Cloud_Local>();
    }
    //Used to generate the Cloud Array Data Counts from x= 0,z= 0 up through (z) height
    //Before moving to 1,0 and counting through z values again
    void InitializeCloudData()
    {
        thisWorldCloud = new worldCloud[mapWidth * mapHeight];
        //Debug.Log("thisWorldCloud[].length = " + thisWorldCloud.Length);
        int tempValue = 0;

        for (int x = 0; x < mapWidth; x++)
        {
            for (int y = 0; y < mapHeight; y++)
            {
                thisWorldCloud[tempValue] = new worldCloud();

                tempValue++;
            }
        }

        tempValue = 0;

        for (int x = 0; x < mapWidth; x++)
        {
            for (int y = 0; y < mapHeight; y++)
            {
                SetNetwork(tempValue);

                tempValue++;
            }
        }
    }
    //Builds the neighbors[] for current cloud position
    void SetNetwork(int thisCloud)
    { 
        int nint = thisCloud + 1;

        if (nint > 0 && thisWorldCloud.Length > nint)
        { thisWorldCloud[thisCloud].SetANeighbor(0, thisWorldCloud[nint]); }

        int wint = thisCloud - mapHeight;
        if (wint > 0 && thisWorldCloud.Length > wint)
        { thisWorldCloud[thisCloud].SetANeighbor(1, thisWorldCloud[wint]); }

        int eint = thisCloud + mapHeight;
        if (eint > 0 && thisWorldCloud.Length > eint)
        { thisWorldCloud[thisCloud].SetANeighbor(2, thisWorldCloud[eint]); }

        int sint = thisCloud - 1;
        if (sint > 0 && thisWorldCloud.Length > sint)
        { thisWorldCloud[thisCloud].SetANeighbor(3, thisWorldCloud[sint]); }
    }
    //Registers the X, Z offset from the playerCloud position for each local cloud
    void InitializeLocalClouds()
    {
        for (int i = 0; i < localClouds.Length; i++)
        {
            int tempOffset = FindCloud(new Vector2(localClouds[i].transform.position.x, localClouds[i].transform.position.z)) - FindCloud(new Vector2(transform.position.x, transform.position.z));
            localClouds[i].SetArrayOffset(tempOffset);
        }
    }
    //Moves the localClouds to the player's position and updates them
    void FirstCenterClouds()
    {
        if (pTrans != null && CheckPlayerLocation() == false)
        {
            transform.position = new Vector3(curX, transform.position.y, curZ);
            UpdateClouds(); 
        }
    } 
    #endregion


    // Update is called once per frame
    void Update()
    {
        CenterClouds();
    }

    //Checks the player location vs stored values and updates CloudManager position to match
    void CenterClouds()
    {
        if (pTrans != null && CheckPlayerLocation() == false)
        { 
            transform.position = new Vector3(curX, transform.position.y, curZ);
            UpdateClouds();
            TriggerCascadeCheck();
        } 
    }

    //Checks the player location, updating values and returning false if they don't match
    bool CheckPlayerLocation()
    {
        Vector3 tempPos = pTrans.position;

        //Rounds the player's Z and X positions
        int pzValue = Mathf.RoundToInt(tempPos.z);
        int pxValue = Mathf.RoundToInt(tempPos.x);

        //Compares the player's rounded Z and X positions to stored values, returning true if they match.
        if (curX == pxValue && curZ == pzValue)
        { 
            return true;//Returning true prevents the script from updating local cloud
        }
        //Updates stored values to current x and Z values, then updates the stored cloud
        else
        { 
            curX = pxValue;
            curZ = pzValue;
            curCloud = FindCloud(new Vector2(pxValue, pzValue));

            return false;//Returning false causes the local cloud to find their 
        }
    }

    //Finds the enemy's cloud and checks returns if it is revealsed or not
    public bool EnemyCloudRevealed(Vector3 myT)
    {
        //Rounds the enemy's x and z values and stores them
        int ezValue = Mathf.RoundToInt(myT.z);
        int exValue = Mathf.RoundToInt(myT.x);

        Cloud_Local.CloudStates myState;

        //uses the rounded x and z values to determine which localCloud space the Enemy occupies
        //if the localCloud value isn't null it retrieves the cloud's state and stores it
        if (thisWorldCloud[FindCloud(new Vector2(exValue, ezValue))].GetMyLocalCloud() != null)
        { myState = thisWorldCloud[FindCloud(new Vector2(exValue, ezValue))].GetMyLocalCloud().GetCurState(); }
        //if the localCloud value returned null it saves the state as active automatically
        else
        { myState = Cloud_Local.CloudStates.Active; }
         
        //If the state is shrinking (meaning the cloud is or is being revealed returns true, allowing the enemy to look for the player
        //When this returns true the enemy will also activate its renderers to appear on screen if they're currently off
        if (myState == Cloud_Local.CloudStates.Shrinking)
        { return true; }
        //Else returns false, preventing the enemy from looking for the player and turning off renderers if they are active
        else
        { return false; }
    }

    //Returns the array value of the cloud corresponding to a given x,z value
    int FindCloud(Vector2 v)
    {
        int myValue = Mathf.RoundToInt(v.y + (v.x * mapHeight));
        return myValue;
    }
    //Takes in Array Value as int and finds X,Z coordinates 
    Vector2 FindCloudCoordinates(int i)
    { 
        int xvalue = Mathf.FloorToInt(i / mapHeight); 
        int zvalue = i - (xvalue * mapHeight); 

        return new Vector2(xvalue, zvalue);
    }

    //Updates the WorldCloud data and then updates the local cloud data
    void UpdateClouds()
    {
        UpdateCloudData();
        UpdateLocalClouds(); 
    }
    //Updates the WorldCloud data 
    void UpdateCloudData()
    {
        for (int i = 0; i < localClouds.Length; i++)
        { 
            Vector2 oldCoordinates = FindCloudCoordinates(localClouds[i].myCurWorldCloud); 

            if (oldCoordinates.x > curX + (((cloudWidth - 1) * .5f)) || oldCoordinates.x < curX - (((cloudWidth - 1) * .5f)))
            { thisWorldCloud[i].DefineThisCloud(new Vector3(1, 1, 1), Cloud_Local.CloudStates.Growing); }
            else if (oldCoordinates.y > curZ + (((cloudHeight - 1) * .5f)) || oldCoordinates.y < curZ - ((cloudHeight - 1) * .5f))
            { thisWorldCloud[i].DefineThisCloud(new Vector3(1, 1, 1), Cloud_Local.CloudStates.Growing); }
            else
            { localClouds[i].UpdateWorldCloud(); } 
        }
    }
    //Updates the local clouds with the new worldCloud data
    void UpdateLocalClouds()
    {
        for (int i = 0; i < localClouds.Length; i++)
        { localClouds[i].SetMyCloud(curCloud + localClouds[i].GetArrayOffset()); }
    }

    //Runs a series of checks on the localClouds to determine which should be revealed and which should be hidden
    void TriggerCascadeCheck()
    { 
        //If the player is within range of the FoW - Should always be true
        if (thisWorldCloud.Length > curCloud && thisWorldCloud[curCloud].GetMyLocalCloud() != null)
        {
            if (thisWorldCloud[curCloud].GetMyState() != Cloud_Local.CloudStates.Shrinking)
            { thisWorldCloud[curCloud].GetMyLocalCloud().EnterShrinking(); }

            thisWorldCloud[curCloud].GetMyLocalCloud().CascadeChecks(4, 0 + 1, new Vector3 (transform.position.x, transform.position.y, transform.position.z));
        }
    }
}
