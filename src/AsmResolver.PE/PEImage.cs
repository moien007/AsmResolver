using System;
using System.Collections.Generic;
using System.Threading;
using AsmResolver.PE.Debug;
using AsmResolver.PE.DotNet;
using AsmResolver.PE.Exports;
using AsmResolver.PE.File;
using AsmResolver.PE.File.Headers;
using AsmResolver.PE.Imports;
using AsmResolver.PE.Relocations;
using AsmResolver.PE.Win32Resources;

namespace AsmResolver.PE
{
    /// <summary>
    /// Provides an implementation for a portable executable (PE) image.
    /// </summary>
    public class PEImage : IPEImage
    {
        private IList<IImportedModule> _imports;
        private readonly LazyVariable<IExportDirectory> _exports;
        private readonly LazyVariable<IResourceDirectory> _resources;
        private IList<BaseRelocation> _relocations;
        private readonly LazyVariable<IDotNetDirectory> _dotNetDirectory;
        private IList<DebugDataEntry> _debugData;
        
        /// <summary>
        /// Opens a PE image from a specific file on the disk.
        /// </summary>
        /// <param name="filePath">The </param>
        /// <returns>The PE image that was opened.</returns>
        /// <exception cref="BadImageFormatException">Occurs when the file does not follow the PE file format.</exception>
        public static IPEImage FromFile(string filePath) => FromFile(PEFile.FromFile(filePath));

        /// <summary>
        /// Opens a PE image from a specific file on the disk.
        /// </summary>
        /// <param name="filePath">The </param>
        /// <param name="readerParameters">The parameters to use while reading the PE image.</param>
        /// <returns>The PE image that was opened.</returns>
        /// <exception cref="BadImageFormatException">Occurs when the file does not follow the PE file format.</exception>
        public static IPEImage FromFile(string filePath, PEReaderParameters readerParameters) => 
            FromFile(PEFile.FromFile(filePath), readerParameters);

        /// <summary>
        /// Opens a PE image from a buffer.
        /// </summary>
        /// <param name="bytes">The bytes to interpret.</param>
        /// <returns>The PE image that was opened.</returns>
        /// <exception cref="BadImageFormatException">Occurs when the file does not follow the PE file format.</exception>
        public static IPEImage FromBytes(byte[] bytes) => FromFile(PEFile.FromBytes(bytes));

        /// <summary>
        /// Opens a PE image from a buffer.
        /// </summary>
        /// <param name="bytes">The bytes to interpret.</param>
        /// <param name="readerParameters">The parameters to use while reading the PE image.</param>
        /// <returns>The PE image that was opened.</returns>
        /// <exception cref="BadImageFormatException">Occurs when the file does not follow the PE file format.</exception>
        public static IPEImage FromBytes(byte[] bytes, PEReaderParameters readerParameters) => 
            FromFile(PEFile.FromBytes(bytes), readerParameters);

        /// <summary>
        /// Opens a PE image from an input stream.
        /// </summary>
        /// <param name="reader">The input stream.</param>
        /// <param name="mode">Indicates the input PE is in its mapped or unmapped form.</param>
        /// <returns>The PE image that was opened.</returns>
        /// <exception cref="BadImageFormatException">Occurs when the file does not follow the PE file format.</exception>
        public static IPEImage FromReader(IBinaryStreamReader reader, PEMappingMode mode = PEMappingMode.Unmapped) => 
            FromFile(PEFile.FromReader(reader, mode));

        /// <summary>
        /// Opens a PE image from an input stream.
        /// </summary>
        /// <param name="reader">The input stream.</param>
        /// <param name="mode">Indicates the input PE is in its mapped or unmapped form.</param>
        /// <param name="readerParameters">The parameters to use while reading the PE image.</param>
        /// <returns>The PE image that was opened.</returns>
        /// <exception cref="BadImageFormatException">Occurs when the file does not follow the PE file format.</exception>
        public static IPEImage FromReader(IBinaryStreamReader reader, PEMappingMode mode, PEReaderParameters readerParameters) => 
            FromFile(PEFile.FromReader(reader, mode), readerParameters);

        /// <summary>
        /// Opens a PE image from a PE file object.
        /// </summary>
        /// <param name="peFile">The PE file object.</param>
        /// <returns>The PE image that was opened.</returns>
        /// <exception cref="BadImageFormatException">Occurs when the file does not follow the PE file format.</exception>
        public static IPEImage FromFile(IPEFile peFile) => FromFile(peFile, new PEReaderParameters());

        /// <summary>
        /// Opens a PE image from a PE file object.
        /// </summary>
        /// <param name="peFile">The PE file object.</param>
        /// <param name="readerParameters">The parameters to use while reading the PE image.</param>
        /// <returns>The PE image that was opened.</returns>
        /// <exception cref="BadImageFormatException">Occurs when the file does not follow the PE file format.</exception>
        public static IPEImage FromFile(IPEFile peFile, PEReaderParameters readerParameters) =>
            new SerializedPEImage(peFile, readerParameters);

        /// <summary>
        /// Initializes a new PE image.
        /// </summary>
        public PEImage()
        {
            _exports = new LazyVariable<IExportDirectory>(GetExports);
            _resources = new LazyVariable<IResourceDirectory>(GetResources);
            _dotNetDirectory = new LazyVariable<IDotNetDirectory>(GetDotNetDirectory);
        }

        /// <inheritdoc />
        public string FilePath
        {
            get;
            protected set;
        }

        /// <inheritdoc />
        public MachineType MachineType
        {
            get;
            set;
        } = MachineType.I386;

        /// <inheritdoc />
        public Characteristics Characteristics
        {
            get;
            set;
        } = Characteristics.Image | Characteristics.LargeAddressAware;

        /// <inheritdoc />
        public DateTime TimeDateStamp
        {
            get;
            set;
        } = new DateTime(1970, 1, 1);

        /// <inheritdoc />
        public OptionalHeaderMagic PEKind
        {
            get;
            set;
        } = OptionalHeaderMagic.Pe32;

        /// <inheritdoc />
        public SubSystem SubSystem
        {
            get;
            set;
        } = SubSystem.WindowsCui;

        /// <inheritdoc />
        public DllCharacteristics DllCharacteristics
        {
            get;
            set;
        } = DllCharacteristics.DynamicBase | DllCharacteristics.NoSeh | DllCharacteristics.NxCompat
            | DllCharacteristics.TerminalServerAware;

        /// <inheritdoc />
        public ulong ImageBase
        {
            get;
            set;
        } = 0x00400000;

        /// <inheritdoc />
        public IList<IImportedModule> Imports
        {
            get
            {
                if (_imports is null) 
                    Interlocked.CompareExchange(ref _imports, GetImports(), null);
                return _imports;
            }
        }

        /// <inheritdoc />
        public IExportDirectory Exports
        {
            get => _exports.Value;
            set => _exports.Value = value;
        }

        /// <inheritdoc />
        public IResourceDirectory Resources
        {
            get => _resources.Value;
            set => _resources.Value = value;
        }

        /// <inheritdoc />
        public IList<BaseRelocation> Relocations
        {
            get
            {
                if (_relocations is null)
                    Interlocked.CompareExchange(ref _relocations, GetRelocations(), null);
                return _relocations;
            }
        }

        /// <inheritdoc />
        public IDotNetDirectory DotNetDirectory
        {
            get => _dotNetDirectory.Value;
            set => _dotNetDirectory.Value = value;
        }

        /// <inheritdoc />
        public IList<DebugDataEntry> DebugData
        {
            get
            {
                if (_debugData is null)
                    Interlocked.CompareExchange(ref _debugData, GetDebugData(), null);
                return _debugData;
            }
        }

        /// <summary>
        /// Obtains the list of modules that were imported into the PE.
        /// </summary>
        /// <returns>The imported modules.</returns>
        /// <remarks>
        /// This method is called upon initialization of the <see cref="Imports"/> property.
        /// </remarks>
        protected virtual IList<IImportedModule> GetImports() => new List<IImportedModule>();

        /// <summary>
        /// Obtains the list of symbols that were exported from the PE.
        /// </summary>
        /// <returns>The exported symbols.</returns>
        /// <remarks>
        /// This method is called upon initialization of the <see cref="Exports"/> property.
        /// </remarks>
        protected virtual IExportDirectory GetExports() => null;

        /// <summary>
        /// Obtains the root resource directory in the PE.
        /// </summary>
        /// <returns>The root resource directory.</returns>
        /// <remarks>
        /// This method is called upon initialization of the <see cref="Resources"/> property.
        /// </remarks>
        protected virtual IResourceDirectory GetResources() => null;

        /// <summary>
        /// Obtains the base relocation blocks in the PE. 
        /// </summary>
        /// <returns>The base relocation blocks.</returns>
        /// <remarks>
        /// This method is called upon initialization of the <see cref="Relocations"/> property.
        /// </remarks>
        protected virtual IList<BaseRelocation> GetRelocations() => new List<BaseRelocation>();

        /// <summary>
        /// Obtains the data directory containing the CLR 2.0 header of a .NET binary.
        /// </summary>
        /// <returns>The data directory.</returns>
        /// <remarks>
        /// This method is called upon initialization of the <see cref="DotNetDirectory"/> property.
        /// </remarks>
        protected virtual IDotNetDirectory GetDotNetDirectory() => null;

        /// <summary>
        /// Obtains the debug data entries in the PE. 
        /// </summary>
        /// <returns>The debug data entries.</returns>
        /// <remarks>
        /// This method is called upon initialization of the <see cref="DebugData"/> property.
        /// </remarks>
        protected virtual IList<DebugDataEntry> GetDebugData() => new List<DebugDataEntry>();
    }
}