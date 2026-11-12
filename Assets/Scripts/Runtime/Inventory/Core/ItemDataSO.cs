using UnityEngine;

[CreateAssetMenu(fileName = "New ItemData", menuName = "Inventory/ItemData")]
public class ItemDataSO : ScriptableObject
{
    [SerializeField, HideInInspector] private string id;
    public string Id => id;

    public string displayName = "Item";
    public Sprite icon;
    public int stackSize = 16;
    public GameObject worldPrefab;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (string.IsNullOrEmpty(id))
        {
            id = System.Guid.NewGuid().ToString();
            UnityEditor.EditorUtility.SetDirty(this);
        }
    }
#endif
}