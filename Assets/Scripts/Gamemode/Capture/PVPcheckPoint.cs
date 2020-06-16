using System;
using Assets.Scripts.Gamemode;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Characters.Titan;
using Assets.Scripts.Gamemode.Settings;
using UnityEngine;
using Random = UnityEngine.Random;

public class PVPcheckPoint : Photon.MonoBehaviour
{
    private bool annie;
    public List<GameObject> nextCheckpoints;
    public List<GameObject> previousCheckpoints = new List<GameObject>();
    public static ArrayList chkPts;
    private float getPtsInterval = 20f;
    private float getPtsTimer;
    public bool hasAnnie;
    private float hitTestR = 15f;
    public GameObject humanCyc;
    public float humanPt;
    public float humanPtMax = 40f;
    public int id;
    public bool isBase;
    public int normalTitanRate = 70;
    private bool playerOn;
    public float size = 1f;
    private float spawnTitanTimer;
    public CheckPointState state;
    private GameObject supply;
    private float syncInterval = 0.6f;
    private float syncTimer;
    public GameObject titanCyc;
    public float titanInterval = 30f;
    private bool titanOn;
    public float titanPt;
    public float titanPtMax = 40f;
    private readonly CaptureGamemode gamemode = FengGameManagerMKII.Gamemode as CaptureGamemode; 
    private readonly FengGameManagerMKII gameManager = FengGameManagerMKII.instance;

    [PunRPC]
    private void changeHumanPt(float pt)
    {
        this.humanPt = pt;
    }

    [PunRPC]
    private void changeState(int num)
    {
        state = (CheckPointState) num;
    }

    [PunRPC]
    private void changeTitanPt(float pt)
    {
        this.titanPt = pt;
    }

    private void checkIfBeingCapture()
    {
        int num;
        this.playerOn = false;
        this.titanOn = false;
        GameObject[] objArray = GameObject.FindGameObjectsWithTag("Player");
        GameObject[] objArray2 = GameObject.FindGameObjectsWithTag("titan");
        for (num = 0; num < objArray.Length; num++)
        {
            if (Vector3.Distance(objArray[num].transform.position, base.transform.position) < this.hitTestR)
            {
                this.playerOn = true;
                if ((this.state == CheckPointState.Human) && objArray[num].GetPhotonView().isMine)
                {
                    if (gameManager.checkpoint != gameObject)
                    {
                        gameManager.checkpoint = gameObject;
                        FengGameManagerMKII.instance.chatRoom.AddLine("<color=#A8FF24>Respawn point changed to point" + this.id + "</color>");
                    }
                    break;
                }
            }
        }
        for (num = 0; num < objArray2.Length; num++)
        {
            if ((Vector3.Distance(objArray2[num].transform.position, base.transform.position) < (this.hitTestR + 5f)) && ((objArray2[num].GetComponent<MindlessTitan>() == null) || objArray2[num].GetComponent<MindlessTitan>().IsAlive))
            {
                this.titanOn = true;
                if (((this.state == CheckPointState.Titan) && objArray2[num].GetPhotonView().isMine) && ((objArray2[num].GetComponent<PlayerTitan>() != null)))
                {
                    if (gameManager.checkpoint != base.gameObject)
                    {
                        gameManager.checkpoint = base.gameObject;
                        FengGameManagerMKII.instance.chatRoom.AddLine("<color=#A8FF24>Respawn point changed to point" + this.id + "</color>");
                    }
                    break;
                }
            }
        }
    }

    private bool checkIfHumanWins()
    {
        for (int i = 0; i < chkPts.Count; i++)
        {
            if ((chkPts[i] as PVPcheckPoint).state != CheckPointState.Human)
            {
                return false;
            }
        }
        return true;
    }

    private bool checkIfTitanWins()
    {
        for (int i = 0; i < chkPts.Count; i++)
        {
            if ((chkPts[i] as PVPcheckPoint).state != CheckPointState.Titan)
            {
                return false;
            }
        }
        return true;
    }

    private float getHeight(Vector3 pt)
    {
        RaycastHit hit;
        LayerMask mask2 = ((int) 1) << LayerMask.NameToLayer("Ground");
        if (Physics.Raycast(pt, -Vector3.up, out hit, 1000f, mask2.value))
        {
            return hit.point.y;
        }
        return 0f;
    }

    public string getStateString()
    {
        if (this.state == CheckPointState.Human)
        {
            return $"<color=#{ColorSet.color_human}>H</color>[-]";
        }
        if (this.state == CheckPointState.Titan)
        {
            return $"<color=#{ColorSet.color_titan_player}>T</color>[-]";
        }
        return $"<color=#{ColorSet.color_D}>_</color>[-]";
    }

    private void humanGetsPoint()
    {
        if (this.humanPt >= this.humanPtMax)
        {
            this.humanPt = this.humanPtMax;
            this.titanPt = 0f;
            this.syncPts();
            this.state = CheckPointState.Human;
            photonView.RPC<int>(changeState, PhotonTargets.All, 1);
            if (((CaptureGamemodeSettings)gamemode.Settings).SpawnSupplyStationOnHumanCapture)
            {
                supply = PhotonNetwork.Instantiate("aot_supply", transform.position - (Vector3.up * (transform.position.y - getHeight(transform.position))), transform.rotation, 0);
            }
            
            gamemode.AddHumanScore(2);
            if (this.checkIfHumanWins())
            {
                GameObject.Find("MultiplayerManager").GetComponent<FengGameManagerMKII>().gameWin2();
            }
        }
        else
        {
            this.humanPt += Time.deltaTime;
        }
    }

    private void humanLosePoint()
    {
        if (this.humanPt > 0f)
        {
            this.humanPt -= Time.deltaTime * 3f;
            if (this.humanPt <= 0f)
            {
                this.humanPt = 0f;
                this.syncPts();
                if (this.state != CheckPointState.Titan)
                {
                    this.state = CheckPointState.Non;
                    photonView.RPC<int>(changeState, PhotonTargets.Others, 0);
                }
            }
        }
    }

    private void newTitan()
    {
        gamemode.SpawnCheckpointTitan(this, base.transform.position - ((Vector3)(Vector3.up * (base.transform.position.y - this.getHeight(base.transform.position)))), base.transform.rotation);
    }

    private void Awake()
    {
        if (gamemode == null)
        {
            DestroyImmediate(gameObject);
            return;
        }
        SetPreviousCheckpoints();
        chkPts.Add(this);
        IComparer comparer = new IComparerPVPchkPtID();
        chkPts.Sort(comparer);
        if (this.humanPt == this.humanPtMax)
        {
            this.state = CheckPointState.Human;
            if (base.photonView.isMine && (FengGameManagerMKII.Level.SceneName != "The City I"))
            {
                this.supply = PhotonNetwork.Instantiate("aot_supply", base.transform.position - ((Vector3) (Vector3.up * (base.transform.position.y - this.getHeight(base.transform.position)))), base.transform.rotation, 0);
            }
        }
        else if (base.photonView.isMine && !this.hasAnnie)
        {
            if (UnityEngine.Random.Range(0, 100) < 50)
            {
                int num = UnityEngine.Random.Range(1, 2);
                for (int i = 0; i < num; i++)
                {
                    this.newTitan();
                }
            }
            if (this.isBase)
            {
                this.newTitan();
            }
        }
        if (this.titanPt == this.titanPtMax)
        {
            this.state = CheckPointState.Titan;
        }
        this.hitTestR = 15f * this.size;
        base.transform.localScale = new Vector3(this.size, this.size, this.size);
    }

    private void SetPreviousCheckpoints()
    {
        foreach (var checkpoint in nextCheckpoints.Select(nextCheckpoint => nextCheckpoint.gameObject.GetComponent<PVPcheckPoint>()))
        {
            checkpoint.previousCheckpoints.Add(gameObject);
        }
    }

    private void syncPts()
    {
        photonView.RPC<float>(changeTitanPt, PhotonTargets.Others, titanPt);
        photonView.RPC<float>(changeHumanPt, PhotonTargets.Others, humanPt);
    }

    private void titanGetsPoint()
    {
        if (this.titanPt >= this.titanPtMax)
        {
            this.titanPt = this.titanPtMax;
            this.humanPt = 0f;
            this.syncPts();
            if ((this.state == CheckPointState.Human) && (this.supply != null))
            {
                PhotonNetwork.Destroy(this.supply);
            }
            this.state = CheckPointState.Titan;
            photonView.RPC<int>(changeState, PhotonTargets.All, 2);
            gamemode.AddTitanScore(2);
            if (this.checkIfTitanWins())
            {
                GameObject.Find("MultiplayerManager").GetComponent<FengGameManagerMKII>().gameLose2();
            }
            if (this.hasAnnie)
            {
                if (!this.annie)
                {
                    this.annie = true;
                    PhotonNetwork.Instantiate("FEMALE_TITAN", base.transform.position - ((Vector3) (Vector3.up * (base.transform.position.y - this.getHeight(base.transform.position)))), base.transform.rotation, 0);
                }
                else
                {
                    this.newTitan();
                }
            }
            else
            {
                this.newTitan();
            }
        }
        else
        {
            this.titanPt += Time.deltaTime;
        }
    }

    private void titanLosePoint()
    {
        if (this.titanPt > 0f)
        {
            this.titanPt -= Time.deltaTime * 3f;
            if (this.titanPt <= 0f)
            {
                this.titanPt = 0f;
                this.syncPts();
                if (this.state != CheckPointState.Human)
                {
                    this.state = CheckPointState.Non;
                    photonView.RPC<int>(changeState, PhotonTargets.All, 0);
                }
            }
        }
    }

    private void Update()
    {
        float x = this.humanPt / this.humanPtMax;
        float num2 = this.titanPt / this.titanPtMax;
        if (!base.photonView.isMine)
        {
            x = this.humanPt / this.humanPtMax;
            num2 = this.titanPt / this.titanPtMax;
            this.humanCyc.transform.localScale = new Vector3(x, x, 1f);
            this.titanCyc.transform.localScale = new Vector3(num2, num2, 1f);
            this.syncTimer += Time.deltaTime;
            if (this.syncTimer > this.syncInterval)
            {
                this.syncTimer = 0f;
                this.checkIfBeingCapture();
            }
        }
        else
        {
            if (this.state == CheckPointState.Non)
            {
                if (this.playerOn && !this.titanOn)
                {
                    this.humanGetsPoint();
                    this.titanLosePoint();
                }
                else if (this.titanOn && !this.playerOn)
                {
                    this.titanGetsPoint();
                    this.humanLosePoint();
                }
                else
                {
                    this.humanLosePoint();
                    this.titanLosePoint();
                }
            }
            else if (this.state == CheckPointState.Human)
            {
                if (this.titanOn && !this.playerOn)
                {
                    this.titanGetsPoint();
                }
                else
                {
                    this.titanLosePoint();
                }
                this.getPtsTimer += Time.deltaTime;
                if (this.getPtsTimer > this.getPtsInterval)
                {
                    this.getPtsTimer = 0f;
                    if (!this.isBase)
                    {
                        gamemode.AddHumanScore(1);
                    }
                }
            }
            else if (this.state == CheckPointState.Titan)
            {
                if (this.playerOn && !this.titanOn)
                {
                    this.humanGetsPoint();
                }
                else
                {
                    this.humanLosePoint();
                }
                this.getPtsTimer += Time.deltaTime;
                if (this.getPtsTimer > this.getPtsInterval)
                {
                    this.getPtsTimer = 0f;
                    if (!this.isBase)
                    {
                        gamemode.AddTitanScore(1);
                    }
                }
                this.spawnTitanTimer += Time.deltaTime;
                if (this.spawnTitanTimer > this.titanInterval)
                {
                    this.spawnTitanTimer = 0f;
                    if (GameObject.FindGameObjectsWithTag("titan").Length < gamemode.Settings.TitanLimit)
                    {
                        this.newTitan();
                    }
                }
            }
            this.syncTimer += Time.deltaTime;
            if (this.syncTimer > this.syncInterval)
            {
                this.syncTimer = 0f;
                this.checkIfBeingCapture();
                this.syncPts();
            }
            x = this.humanPt / this.humanPtMax;
            num2 = this.titanPt / this.titanPtMax;
            this.humanCyc.transform.localScale = new Vector3(x, x, 1f);
            this.titanCyc.transform.localScale = new Vector3(num2, num2, 1f);
        }
    }

    public GameObject chkPtNext => nextCheckpoints.Count <= 0 ? null : nextCheckpoints[Random.Range(0, nextCheckpoints.Count)];

    public GameObject chkPtPrevious => previousCheckpoints.Count <= 0 ? null : previousCheckpoints[Random.Range(0, previousCheckpoints.Count)];
}
