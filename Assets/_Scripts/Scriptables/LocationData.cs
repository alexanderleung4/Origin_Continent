using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewLocation", menuName = "Origin/Location Data")]
public class LocationData : ScriptableObject
{
    [Header("Basic Info")]
    public string locationID;
    public string locationName;

    [Header("Home Settings (家园设定)")]
    public bool isHomeLocation = false; // 勾选此项代表这是个安全屋/家
    [TextArea] public string description;

    [Header("Visuals (视觉与场景)")]
    [Tooltip("完整的场景预制体（优先加载）")]
    public GameObject mapPrefab; 
    [Tooltip("2D背景图（当没有 mapPrefab 时作为备用）")]
    public Sprite backgroundImage;
    public AudioClip backgroundMusic;
    
    [Header("Navigation")]
    public List<LocationData> connectedLocations;

    [Header("Population (常驻NPC)")]
    // 如果使用了 mapPrefab，建议将 NPC 直接做进 Prefab 里。
    // 这里保留是为了向下兼容旧的纯 2D 随机生成模式。
    public List<CharacterData> staticNPCs; 

    [Header("Interactions")]
    public bool isSafeZone = true;
}