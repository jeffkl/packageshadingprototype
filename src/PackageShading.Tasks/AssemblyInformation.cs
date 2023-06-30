namespace PackageShading.Tasks
{
    public sealed class AssemblyInformation
    {
        public AssemblyReferencesByAssemblyName AssemblyReferences { get; } = new AssemblyReferencesByAssemblyName();

        public FriendAssembliesByInternalsVisibleTo FriendAssemblies { get; } = new FriendAssembliesByInternalsVisibleTo();
    }
}