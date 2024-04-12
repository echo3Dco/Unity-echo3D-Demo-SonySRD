using UnityEngine;
/// <summary>
/// This script is added to all loaded assets during initialization
/// The Start() method is configured to 
///   1. Add a "RemoteTransformation.cs" script to the asset. This script listens for and applies runtime updates based on changes made on the echo3D console.
///   2. Set the name of the gameobject to the name of the asset (the filename of the original file)
/// </summary>
public class CustomBehaviour : MonoBehaviour
{
    [HideInInspector]
    public Entry entry;

    [HideInInspector]
    public bool disableRemoteTransformations = false;


    // Use this for initialization
    void Start()
    {
        // Add RemoteTransformations script to object and set its entry
        if (!disableRemoteTransformations)
        {
            gameObject.AddComponent<RemoteTransformations>().entry = entry;
        }

        
    }

    // Update is called once per frame
    void Update()
    {

        // Qurey additional data to get the name
        string value = "";
        if (entry.getAdditionalData() != null && entry.getAdditionalData().TryGetValue("name", out value))
        {
            // Set name
            gameObject.name = value;
        }
    }
}