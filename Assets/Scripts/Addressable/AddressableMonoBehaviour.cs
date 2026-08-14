using RpgProto.Addressables;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Addressable
{
    public abstract class AddressableMonoBehaviour : MonoBehaviour, IAddressable
    {
        [DontValidate]
        [ReadOnly]
        [SerializeField]
        private Reference _reference;

        public Reference Reference => _reference;

#if UNITY_EDITOR
        [ShowInInspector]
        [PropertyOrder(-1000)]
        private string Guid => _reference.AssetGuid;
#endif

        #region IAddressable

        string IAddressable.Name => name;

        #endregion
    }

    public abstract class AddressableMonoBehaviour<T> : AddressableMonoBehaviour, IAddressable<T>
        where T : class
    {
        #region IAddressable<T>

        Reference<T> IAddressable<T>.Reference => Reference.ToReference<T>();

        #endregion
    }
}