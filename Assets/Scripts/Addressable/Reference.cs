using System;
using Addressable;
using Unity.Assertions;
using UnityEngine;
using UnityEngine.Serialization;

namespace RpgProto.Addressables
{
    [Serializable]
    public struct Reference : IComparable<Reference>
    {
        [FormerlySerializedAs("m_assetGUID")]
        [FormerlySerializedAs("m_AssetGUID")]
        [SerializeField]
        private string _assetGuid;

        public Reference(string assetGuid)
        {
            Assert.IsTrue(Guid.TryParse(assetGuid, out _));

            _assetGuid = assetGuid;
        }

        public readonly string AssetGuid => _assetGuid ?? string.Empty;

        public readonly bool HasValidAssetGuid => Guid.TryParse(AssetGuid, out _);

        public static bool operator ==(Reference x, Reference y) => x.AssetGuid == y.AssetGuid;

        public static bool operator !=(Reference x, Reference y) => x.AssetGuid != y.AssetGuid;

        public readonly override bool Equals(object other)
        {
            return other != null && other.GetHashCode() == GetHashCode();
        }

        public readonly override int GetHashCode()
        {
            return AssetGuid.GetHashCode();
        }

        public readonly Reference<T> ToReference<T>()
            where T : class
        {
            return string.IsNullOrWhiteSpace(AssetGuid) ? default : new Reference<T>(AssetGuid);
        }

        public readonly override string ToString()
        {
            return AssetGuid;
        }

        #region IComparable<Reference<T>>

        int IComparable<Reference>.CompareTo(Reference other)
        {
            return ReferenceUtility.Compare(this, other);
        }

        #endregion
    }

    [Serializable]
    public struct Reference<T> : IComparable<Reference<T>>
        where T : class
    {
        [FormerlySerializedAs("m_assetGUID")]
        [FormerlySerializedAs("m_AssetGUID")]
        [SerializeField]
        private string _assetGuid;

        public Reference(string assetGuid)
        {
            Assert.IsTrue(string.IsNullOrEmpty(assetGuid) || Guid.TryParse(assetGuid, out _));

            _assetGuid = assetGuid;
        }

        public readonly string AssetGuid => _assetGuid ?? string.Empty;

        public readonly bool HasValidAssetGuid => Guid.TryParse(AssetGuid, out _);

        public static bool operator ==(Reference<T> x, Reference y) => x.AssetGuid == y.AssetGuid;

        public static bool operator ==(Reference<T> x, Reference<T> y) => x.AssetGuid == y.AssetGuid;

        public static bool operator !=(Reference<T> x, Reference y) => x.AssetGuid != y.AssetGuid;

        public static bool operator !=(Reference<T> x, Reference<T> y) => x.AssetGuid != y.AssetGuid;

        public static implicit operator Reference(Reference<T> value)
        {
            return value.ToReference();
        }

        public readonly override bool Equals(object other)
        {
            return other != null && other.GetHashCode() == GetHashCode();
        }

        public readonly override int GetHashCode()
        {
            return AssetGuid.GetHashCode();
        }

        public readonly Reference ToReference()
        {
            return string.IsNullOrWhiteSpace(AssetGuid) ? default : new Reference(AssetGuid);
        }

        public readonly Reference<TRefence> ToReference<TRefence>()
            where TRefence : class
        {
            return string.IsNullOrWhiteSpace(AssetGuid) ? default : new Reference<TRefence>(AssetGuid);
        }

        public readonly override string ToString()
        {
            return AssetGuid;
        }

        #region IComparable<Reference<T>>

        int IComparable<Reference<T>>.CompareTo(Reference<T> other)
        {
            return ReferenceUtility.Compare(this, other);
        }

        #endregion
    }
}