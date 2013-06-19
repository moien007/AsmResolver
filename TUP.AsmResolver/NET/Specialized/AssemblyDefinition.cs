﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TUP.AsmResolver.NET.Specialized
{
    public class AssemblyDefinition : AssemblyReference
    {
        string name;
        string culture;

        public AssemblyDefinition(MetaDataRow row)
            : base(row)
        {
        }

        public AssemblyDefinition(string name, AssemblyAttributes attributes, Version version, AssemblyHashAlgorithm hashAlgorithm, uint publicKey, string culture)
            : base(new MetaDataRow(
                (uint)hashAlgorithm,
                (byte)version.Major,
                (byte)version.Minor,
                (byte)version.Build,
                (byte)version.Revision,
                (uint)attributes,
                publicKey,
                0U,
                0U))
        {
            this.name = name;
            this.culture = culture;
        }

        public override AssemblyHashAlgorithm HashAlgorithm
        {
            get { return (AssemblyHashAlgorithm)Convert.ToUInt32(metadatarow.parts[0]); }
        }

        public override Version Version
        {
            get
            {
                return new Version(
                Convert.ToInt32(metadatarow.parts[1]),
                Convert.ToInt32(metadatarow.parts[2]),
                Convert.ToInt32(metadatarow.parts[3]),
                Convert.ToInt32(metadatarow.parts[4])
                );
            }
            set
            {
                metadatarow.parts[1] = (ushort)value.Major;
                metadatarow.parts[2] = (ushort)value.Minor;
                metadatarow.parts[3] = (ushort)value.Build;
                metadatarow.parts[4] = (ushort)value.Revision;
            }
        }

        public override AssemblyAttributes Attributes
        {
            get { return (AssemblyAttributes)Convert.ToUInt32(metadatarow.parts[5]); }
        }

        public override uint PublicKeyOrToken
        {
            get { return Convert.ToUInt32(metadatarow.parts[6]); }
        }

        public override string Name
        {
            get
            {
                if (string.IsNullOrEmpty(name))
                    netheader.StringsHeap.TryGetStringByOffset(Convert.ToUInt32(metadatarow.parts[7]), out name);
                return name;
            }
        }

        public override string Culture
        {
            get
            { 
                if (culture == null)
                    netheader.StringsHeap.TryGetStringByOffset(Convert.ToUInt32(metadatarow.parts[8]), out name);
                return culture;
            }
        }

        public override string ToString()
        {
            return Name;
        }

        public override void ClearCache()
        {
            name = null;
            culture = null;
        }

    }
}
