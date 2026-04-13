using System.Collections.Generic;
using UnityEngine;
using ARGallery.Content;

/// <summary>
/// Factory that creates runtime content instances from prefabs.
/// </summary>
public class RuntimeContentFactory
{
    public struct ContentCreateResult
    {
        public bool success;
        public string message;
        public GameObject instance;
        public DraggableObject draggable;
    }

    public ContentCreateResult CreateImageContent(GameObject picturePrefab)
    {
        return CreateFromPrefab(
            picturePrefab,
            RuntimeContentShellType.SurfaceShell,
            "Picture prefab is not assigned.");
    }

    public ContentCreateResult CreateTextContent(GameObject textPrefab, string textToDisplay)
    {
        ContentCreateResult result = CreateFromPrefab(
            textPrefab,
            RuntimeContentShellType.TextShell,
            "Text prefab is not assigned.");
        if (!result.success || result.instance == null)
            return result;

        string text = textToDisplay ?? "";
        TextMesh textMesh = result.instance.GetComponent<TextMesh>();
        if (textMesh != null)
            textMesh.text = text;

        TMPro.TextMeshPro tmp = result.instance.GetComponent<TMPro.TextMeshPro>();
        if (tmp != null)
            tmp.text = text;

        return result;
    }

    public bool ReleaseToPool(GameObject instance)
    {
        if (instance == null)
            return false;
        // reset the object to the shell for later reuse
        RuntimeContentPoolResetter.ResetForRelease(instance);
        // release the object to the pool
        return RuntimeContentPool.Shared.Release(instance);
    }

    private static ContentCreateResult CreateFromPrefab(
        GameObject prefab,
        RuntimeContentShellType shellType,
        string nullMessage)
    {
        if (prefab == null)
        {
            return new ContentCreateResult
            {
                success = false,
                message = nullMessage
            };
        }

        GameObject instance = RuntimeContentPool.Shared.Acquire(shellType, prefab);
        RuntimeContentPoolResetter.ResetForAcquire(instance, shellType);

        return new ContentCreateResult
        {
            success = true,
            message = "Content instance created.",
            instance = instance,
            draggable = instance.GetComponent<DraggableObject>()
        };
    }
}

/// <summary>
/// Pool storage only: acquires/releases shell instances by runtime shell type.
/// Lifecycle reset and payload cleanup are handled by RuntimeContentPoolResetter.
/// </summary>
public class RuntimeContentPool
{
    private static RuntimeContentPool shared;
    public static RuntimeContentPool Shared // get the singleton instance , there is only one pool
    {
        get
        {
            if (shared == null)
                shared = new RuntimeContentPool(); // create the singleton instance if it doesn't exist
            return shared;
        }
    }
    // the actual instance of the pool , the queue of array contains the inactive objects by shell type
    private readonly Dictionary<RuntimeContentShellType, Queue<GameObject>> inactiveByShell =
        new Dictionary<RuntimeContentShellType, Queue<GameObject>>();

    // the root of the inactive objects by shell type
    private readonly Dictionary<RuntimeContentShellType, Transform> inactiveRootByShell =
        new Dictionary<RuntimeContentShellType, Transform>();

    private RuntimeContentPool() { } // private constructor to prevent multiple instances

    public GameObject Acquire(RuntimeContentShellType shellType, GameObject prefab)
    {
        if (prefab == null)
            return null;
        // get the queue of the inactive objects by shell type
        Queue<GameObject> q = GetQueue(shellType);
        while (q.Count > 0) // if there are inactive objects in the queue, use the first one
        {
            // dequeue the first object in the queue
            GameObject existing = q.Dequeue();
            if (existing == null)
                continue;
            // activate the object
            existing.SetActive(true); 
            EnsureTag(existing, shellType); // add the tag to the object
            return existing;
        }

        GameObject created = Object.Instantiate(prefab, Vector3.zero, Quaternion.identity); // if there are no inactive objects in the queue, instantiate a new object
        EnsureTag(created, shellType);
        return created;
    }

    public bool Release(GameObject instance)
    {
        if (instance == null)
            return false;

        PooledContentTag tag = instance.GetComponent<PooledContentTag>();
        if (tag == null) //  that means the object is not pooled(be managed by the pool) , so we destroy it
        {   
            Object.Destroy(instance);
            return false;
        }

        RuntimeContentShellType shellType = tag.shellType;
        instance.transform.SetParent(GetOrCreateInactiveRoot(shellType), false);
        instance.SetActive(false);
        GetQueue(shellType).Enqueue(instance);
        return true;
    }
    // get the queue of the inactive objects by shell type
    private Queue<GameObject> GetQueue(RuntimeContentShellType shellType)
    {
        Queue<GameObject> q;
        // if the queue does not exist for certain shell type, create it
        if (!inactiveByShell.TryGetValue(shellType, out q))
        {
            q = new Queue<GameObject>();
            inactiveByShell[shellType] = q;
        }
        return q;
    }
    // get the root of the inactive objects by shell type
    private Transform GetOrCreateInactiveRoot(RuntimeContentShellType shellType)
    {
        Transform existing;
        if (inactiveRootByShell.TryGetValue(shellType, out existing) && existing != null)
            return existing;
        // if the root does not exist for certain shell type, create it
        GameObject root = new GameObject("RuntimeContentPool_" + shellType);
        root.hideFlags = HideFlags.HideInHierarchy;
        Object.DontDestroyOnLoad(root);

        inactiveRootByShell[shellType] = root.transform;
        return root.transform;
    }
    // add the tag to the object to identify the shell type
    private static void EnsureTag(GameObject instance, RuntimeContentShellType shellType)
    {
        if (instance == null)
            return;
     
        PooledContentTag tag = instance.GetComponent<PooledContentTag>();
        if (tag == null) // if the tag does not exist, add it
            tag = instance.AddComponent<PooledContentTag>();

        tag.shellType = shellType;
    }
}

/// <summary>
/// Runtime marker for safe pool return. Stores shell type on each pooled instance.
/// </summary>
public class PooledContentTag : MonoBehaviour
{
    public RuntimeContentShellType shellType;
}

/// <summary>
/// Lifecycle reset rules for pooled content shells.
/// </summary>
public static class RuntimeContentPoolResetter
{
    public static void ResetForAcquire(GameObject instance, RuntimeContentShellType shellType)
    {
        if (instance == null)
            return;

        instance.SetActive(true);
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one;

        if (shellType == RuntimeContentShellType.ModelShell)
            ClearModelChildren(instance); // defensive reset for model shell
    }

    public static void ResetForRelease(GameObject instance)
    {
        if (instance == null)
            return;

        PooledContentTag tag = instance.GetComponent<PooledContentTag>();
        RuntimeContentShellType shellType = tag != null ? tag.shellType : RuntimeContentShellType.SurfaceShell;
        // clear the content of the object based on the shell type
        switch (shellType)
        {
            case RuntimeContentShellType.SurfaceShell:
                ClearSurfaceTexture(instance);
                break;
            case RuntimeContentShellType.TextShell:
                ClearText(instance);
                break;
            case RuntimeContentShellType.ModelShell:
                ClearModelChildren(instance);
                break;
        }

        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one;
    }



    
    private static void ClearSurfaceTexture(GameObject instance)
    {
        Renderer r = instance.GetComponent<Renderer>();
        if (r != null && r.material != null)
            r.material.mainTexture = null;
    }

    private static void ClearText(GameObject instance)
    {
        TextMesh tm = instance.GetComponent<TextMesh>();
        if (tm != null)
            tm.text = string.Empty;

        TMPro.TextMeshPro tmp = instance.GetComponent<TMPro.TextMeshPro>();
        if (tmp != null)
            tmp.text = string.Empty;
    }

    private static void ClearModelChildren(GameObject instance)
    {
        ModelContentContainerRoot root = instance.GetComponent<ModelContentContainerRoot>();
        Transform body = root != null ? root.ContentBody : instance.transform;
        if (body == null)
            return;

        for (int i = body.childCount - 1; i >= 0; i--)
            Object.Destroy(body.GetChild(i).gameObject);
    }
}
