namespace AsmResolver.DotNet.Signatures.Marshal
{
    /// <summary>
    /// When derived from this class, provides a description on how a specific type needs to be marshaled upon
    /// calling to or from unmanaged code via P/Invoke dispatch.
    /// </summary>
    public abstract class MarshalDescriptor : ExtendableBlobSignature
    {
        /// <summary>
        /// Reads a marshal descriptor signature from the provided input stream.
        /// </summary>
        /// <param name="parentModule">The module that defines the marshal descriptor</param>
        /// <param name="reader">The input stream.</param>
        /// <returns>The marshal descriptor.</returns>
        public static MarshalDescriptor FromReader(ModuleDefinition parentModule, IBinaryStreamReader reader)
        {
            var nativeType = (NativeType) reader.ReadByte();
            MarshalDescriptor descriptor = nativeType switch
            {
                NativeType.SafeArray => SafeArrayMarshalDescriptor.FromReader(parentModule, reader),
                NativeType.FixedArray => FixedArrayMarshalDescriptor.FromReader(reader),
                NativeType.LPArray => LPArrayMarshalDescriptor.FromReader(reader),
                NativeType.CustomMarshaller => CustomMarshalDescriptor.FromReader(parentModule, reader),
                NativeType.FixedSysString => FixedSysStringMarshalDescriptor.FromReader(reader),
                NativeType.Interface => ComInterfaceMarshalDescriptor.FromReader(nativeType, reader),
                NativeType.IDispatch => ComInterfaceMarshalDescriptor.FromReader(nativeType, reader),
                NativeType.IUnknown => ComInterfaceMarshalDescriptor.FromReader(nativeType, reader),
                _ => new SimpleMarshalDescriptor(nativeType)
            };

            descriptor.ExtraData = reader.ReadToEnd();
            return descriptor;
        }
        
        /// <summary>
        /// Gets the native type of the marshal descriptor. This is the byte any descriptor starts with. 
        /// </summary>
        public abstract NativeType NativeType
        {
            get;
        }
    }
}