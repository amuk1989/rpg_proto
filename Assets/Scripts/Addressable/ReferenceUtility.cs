using System;
using RpgProto.Addressables;

namespace Addressable
{
    public static class ReferenceUtility
    {
        public static int Compare(Reference a, Reference b)
        {
            if (a.AssetGuid != null)
            {
                return b.AssetGuid == null
                    ? 1
                    : string.Compare(a.AssetGuid, b.AssetGuid, StringComparison.Ordinal);
            }

            if (b.AssetGuid == null)
            {
                return 0;
            }

            return -1;
        }
    }
}