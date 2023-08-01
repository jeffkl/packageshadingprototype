# Package Shading Prototype


## Shading Transitive Dependencies

This package includes build logic to rename assemblies in your dependency graph that would otherwise be unified by the .NET SDK.  Consider the following project's dependency graph:

```xml
<ItemGroup>
  <PackageReference Include="PackageA" Version="1.0.0" /> <!-- PackageA 1.0.0 -> PackageZ 1.0.0 -->
  <PackageReference Include="PackageB" Version="1.0.0" /> <!-- PackageA 1.0.0 -> PackageZ 2.0.0 -->
</ItemGroup>
```
When `PackageA` and `PackageB` are restored, `PackageZ` version `2.0.0` will be used since it is the highest version.  At runtime, `PackageA` will be forced to use `PackageZ` version `2.0.0` even if it was built and tested
against `PackageZ` version `1.0.0`.  If there have been any breaking runtime changes in PackageZ, PackageA could fail at runtime.  Since only one version of PackageZ can exist in the application directory, it can be difficult
to workaround the issue.

On .NET Framework, you can place `PackageZ.dll` version `1.0.0.0` into a subfolder of the application directory and use assembly binding information in the App.config to tell the runtime where to find it.  This will only work
if the assembly is strong name signed however, since the .NET assembly loader can only load different versions of the same assembly side-by-side if they are strong named signed.

On .NET Core, there is no way to work around this issue.  The .NET assembly loader will always load the highest version of an assembly.  The only path forward is to recompile `PackageA` against `PackageZ` version `2.0.0.0`
which is not always possible.

Package shading provides an escape hatch for the above example.  It renames the `PackageZ` version `1.0.0` assembly so it can exist in the same directory as `PackageZ` version `2.0.0` and updates any assemblies that reference it.

```xml
<ItemGroup>
  <PackageReference Include="PackageShading.Tasks" Version="1.0.3-preview" PrivateAssets="All" />

  <PackageReference Include="PackageA" Version="1.0.0" ShadeDependencies="PackageZ" />
  <PackageReference Include="PackageB" Version="1.0.0" />
</ItemGroup>
```

As the project is built, the package shader logic finds the assemblies in `PackageZ` version `1.0.0` and renames them.  The renamed assembly is then copied to the application output directory.

Now the output folder has both `PackageZ.dll` which is version `2.0.0.0` and `PackageZ.1.0.0.0.dll`.

![image](https://github.com/jeffkl/packageshadingprototype/assets/17556515/390c436a-25f1-4463-ac1f-d1c409bf1c82)

`PackageA.dll` was also updated to reference `PackageZ.1.0.0.0.dll` instead of `PackageZ.dll`.

![image](https://github.com/jeffkl/packageshadingprototype/assets/17556515/4212b728-da51-4456-bf29-49db3aa55217)

## Shading Entire Packages
If you wish to shade all of the assemblies in a particular package and its transitive dependencies, you can use the `Shade` property:
```xml
<ItemGroup>
  <PackageReference Include="Newtonsoft.Json.Bson" Version="1.0.2" Shade="true" />
</ItemGroup>
```

![image](https://github.com/jeffkl/packageshadingprototype/assets/17556515/b4322d58-280e-4af4-85e0-402f3240e137)

![image](https://github.com/jeffkl/packageshadingprototype/assets/17556515/87d89a98-3918-49a2-af97-9c7952f8f184)


## Limitations
Assembly shading can be a great way to fix runtime issues with dependencies, but it does have some limitations.

- Shaded assemblies are strong name signed with a new public key pair.  In the process, Authenticode signatures are lost.
- Since a shaded assembly is loaded side-by-side with a newer version, you will not be able to pass types around between the two versions.  You will get a compile-time error if you attempt to do this.  If you have to pass
  types around between assemblies, shading will not be possible.
- Assembly references will be updated because they are easy to discover but any code that uses reflection to load types will not be updated.  This includes `Assembly.Load` and `Assembly.LoadFrom`.  
  If you have code that uses reflection to load types from a shaded assembly, you will need to update it to use the new assembly name.
