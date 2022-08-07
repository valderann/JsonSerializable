namespace JsonSerialize
{
    public class SerializableObject
    {
        public string AssemblyName { get; set; }
        public string TypeName { get; set; }
        public string Constructor { get; set; }
        public string SerializationError { get; set; }
        public string Namespace { get; set; }
        public SerializationProperty[] Properties { get; set; }
    }

    public class SerializationProperty
    {
        public string Name { get; set; }
        public bool CanWrite { get; set; }
    }
}
