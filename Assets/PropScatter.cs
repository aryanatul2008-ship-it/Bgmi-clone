using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// Randomly scatters prefabs (rocks, bushes, crates...) over the terrain so you don't place every one by hand.
/// Put on an empty object called "Environment" at 0,0,0. Fill in "Props", then right-click the component header
/// (or the three dots) and choose "Scatter Now". Props are created as children, so you can still move or delete any of them.
/// Run "Clear" to remove them and try another seed.
public class PropScatter : MonoBehaviour
{
    [System.Serializable]
    public class PropType
    {
        public GameObject prefab;
        public int count = 20;
        public Vector2 scaleMultiplier = new Vector2(0.7f, 1.6f); // random size relative to the prefab's own scale
        public float minSpacing = 10f;                            // keep this far from other props
        public float yOffset = 0f;                                // negative sinks it into the ground
    }

    public PropType[] props;
    public float areaRadius = 120f;        // scatter inside this circle around this object
    public Transform keepClear;            // e.g. the Player: nothing is placed near it
    public float keepClearRadius = 15f;
    public int seed = 1234;

    [ContextMenu("Scatter Now")]
    public void Scatter()
    {
        Terrain terrain = Terrain.activeTerrain;
        if (terrain == null) { Debug.LogError("PropScatter: no Terrain found in the scene."); return; }

        Clear();
        Random.InitState(seed);
        List<Vector3> placed = new List<Vector3>();
        int total = 0;

        foreach (PropType p in props)
        {
            if (p.prefab == null) continue;
            for (int i = 0; i < p.count; i++)
            {
                for (int tries = 0; tries < 40; tries++)
                {
                    Vector2 c = Random.insideUnitCircle * areaRadius;
                    Vector3 pos = new Vector3(transform.position.x + c.x, 0f, transform.position.z + c.y);
                    pos.y = terrain.SampleHeight(pos) + terrain.GetPosition().y + p.yOffset;

                    if (keepClear != null && Flat(pos, keepClear.position) < keepClearRadius) continue;
                    if (TooClose(pos, placed, p.minSpacing)) continue;

                    // keep the prefab's own tilt (FBX models are often rotated -90) and spin it around world up
                    Quaternion rot = Quaternion.AngleAxis(Random.Range(0f, 360f), Vector3.up) * p.prefab.transform.rotation;
                    GameObject go = Spawn(p.prefab);
                    go.transform.SetPositionAndRotation(pos, rot);
                    go.transform.localScale = p.prefab.transform.localScale * Random.Range(p.scaleMultiplier.x, p.scaleMultiplier.y);
                    placed.Add(pos);
                    total++;
                    break;
                }
            }
        }
        Debug.Log("PropScatter: placed " + total + " props. Now select the Terrain and Bake the NavMesh again.");
    }

    [ContextMenu("Clear")]
    public void Clear()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(transform.GetChild(i).gameObject);
    }

    GameObject Spawn(GameObject prefab)
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            GameObject linked = (GameObject)PrefabUtility.InstantiatePrefab(prefab, transform);
            Undo.RegisterCreatedObjectUndo(linked, "Scatter props");
            return linked;
        }
#endif
        return Instantiate(prefab, transform);
    }

    static float Flat(Vector3 a, Vector3 b)
    {
        a.y = 0f; b.y = 0f;
        return Vector3.Distance(a, b);
    }

    static bool TooClose(Vector3 pos, List<Vector3> others, float spacing)
    {
        for (int i = 0; i < others.Count; i++)
            if (Flat(pos, others[i]) < spacing) return true;
        return false;
    }
}