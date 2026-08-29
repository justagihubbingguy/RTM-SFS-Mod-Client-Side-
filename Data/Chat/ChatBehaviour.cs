using UnityEngine;

public class ChatBehaviour : MonoBehaviour
{
    public static ChatBehaviour Instance;

    public void Awake()
    {
        if (Instance == null) Instance = this;
        
    }

    public void Start()
    {
        
    }
    public void Update()
    {
        
    }
}