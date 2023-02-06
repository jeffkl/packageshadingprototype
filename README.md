# Producer Side Package Shading Prototype

This package was created by shading its dependencies and including them in the package.  The package author must know what assemblies they want to shade
ahead of time.

- The package itself has no dependencies since the runtime dependencies are included inside the package
- The original dependency assemblies have been renamed so they won't collide with other versions in the final output folder
- Since the dependency assemblies were renamed before SampleLibrary was compiled, all of SampleLibrary's references are correct


![image](https://user-images.githubusercontent.com/17556515/136617847-ff2dd5a7-2fcd-4498-81db-c9000e6b8171.png)

## Sample output when consuming a consumer side shaded package
This console app depends on a project that is using shading.  The dependencies end up in the output directory so they can be used but the console application does not know about them.

![image](https://user-images.githubusercontent.com/17556515/136617957-a1cb8860-f89e-4043-a1f4-ff3705a5039a.png)

# Consumer Side Shading Prototype

Consider the below example.  Newtonsoft.Json.Bson 1.0.2 depends on Newtonsoft.Json 12.0.1.  Microsoft.NET.Test.Sdk 17.3.0 depends on Newtonsoft.Json 9.0.0.
During restore, NuGet will unify the Newonsoft.Json dependency with the highest version, this case 12.0.1.  But what if that version has a breaking change?
Microsoft.NET.Test.Sdk and all of its dependencies were built and tested against Newtonsoft.Json 9.0.0.  Let's also suppose that the package owner of
Microsoft.NET.Test.Sdk is no longer going to ship updates and don't want to make it work with a newer version.  The solution is to shade the dependency as 
a consumer of that package.

![image](https://user-images.githubusercontent.com/17556515/213560298-e1c9b197-7a69-41a1-aaa9-1761c3ff493f.png)

Now a consumer can specify that the Newtonsoft.Json dependency of Microsoft.NET.Test.Sdk should be shaded. This will rename the assembly and update all
references to it.

![image](https://user-images.githubusercontent.com/17556515/213561695-c9c10591-e949-42b3-9bbb-f47346a69a20.png)

Now the output folder has both `Newtonsoft.Json.dll` and `Newtonsoft.Json.9.0.0.0.dll`

![image](https://user-images.githubusercontent.com/17556515/213564522-a8c6b7fb-a1d1-49ff-b711-e3831636b568.png)

The assemblies that reference `Newtonsoft.Json` version 9.0.0 were also updated so they load the correct version:

![image](https://user-images.githubusercontent.com/17556515/213562494-4f952813-db1b-4003-ae76-6a3cfd02da9e.png)

And finally, and assembly that referenced an assembly that was updated, has to be re-signed so any referencing assembly also has to be updated (notice
the public key token is different):

![image](https://user-images.githubusercontent.com/17556515/213563964-003290ad-6af9-454b-8149-253c064160fe.png)
