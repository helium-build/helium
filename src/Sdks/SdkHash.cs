using System;
using System.IO;
using System.Threading.Tasks;
using Helium.Util;

namespace Helium.Sdks
{
    public class SdkHash
    {
        public SdkHash(SdkHashType hashType, string hash) {
            HashType = hashType;
            Hash = hash;
        }
        
     
        public SdkHashType HashType { get; }
        public string Hash { get; }

        public Task<bool> Validate(Stream stream) => HashType switch {
            SdkHashType.Sha256 => HashUtil.ValidateSha256(stream, Hash),
            SdkHashType.Sha512 => HashUtil.ValidateSha512(stream, Hash),
            _ => throw new Exception("Unknown hash type"),
        };

    }
}