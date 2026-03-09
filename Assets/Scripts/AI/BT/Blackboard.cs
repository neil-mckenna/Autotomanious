using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

// this is not used it was pulled in from the course I studied
public class Blackboard : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] public float timeOfDay;
    public Text clock;

    public Stack<GameObject> patrons = new Stack<GameObject>();
    public int openTime = 6;
    public int closeTime = 20;


    [SerializeField][Range(0, 20)] public float timeRatioSecondsPerHour = 3f; 


    private static Blackboard _instance;
    public static Blackboard Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<Blackboard>();

                if (_instance == null)
                {
                    GameObject go = new GameObject("Blackboard");
                    _instance = go.AddComponent<Blackboard>();
                    DontDestroyOnLoad(go);
                }
                else
                {
                    DontDestroyOnLoad(_instance.gameObject);
                }
            }

            return _instance;
        }
    }

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (_instance != this)
        {
            Debug.LogWarning("  Duplicate Blackboard destroyed, Only one should exist!");
            Destroy(gameObject);
            return;
        }

    }


    private void Start()
    {
        StartCoroutine(nameof(UpdateClock));

    }


    IEnumerator UpdateClock()
    {
        while (true)
        {
            timeOfDay++;
            if (timeOfDay >= 23)
            {
                timeOfDay = 0;
            }
            clock.text = timeOfDay + ":00";

            if(timeOfDay == closeTime)
            {
                patrons.Clear();
            }

            yield return new WaitForSeconds(timeRatioSecondsPerHour);
        }
    }


    public bool RegisterPatron(GameObject p)
    {
        patrons.Push(p);
        return true;
    }

    public void DeregisterPatron()
    {
        
    }




}
