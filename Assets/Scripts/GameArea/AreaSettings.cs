using RpgProto.Addressables;
using UnityEngine;

namespace GameArea
{
    [CreateAssetMenu(fileName = "AreaSettings", menuName = "Settings/AreaSettings", order = 0)]
    public class AreaSettings : ScriptableObject
    {
        [SerializeField] private Reference<AreaBlock> _blockPrefab;
        public Reference<AreaBlock> BlockPrefab => _blockPrefab;
    }
}