# Package Shading Prototype
A prototype assembly shader for rewriting dependent assemblies and shading/hiding them from consumers


# Sample Package
This package was created by shading its dependencies and including them in the package
![image](https://user-images.githubusercontent.com/17556515/136617847-ff2dd5a7-2fcd-4498-81db-c9000e6b8171.png)

# Sample Project-to-Project
This console app depends on a project that is using shading.  The dependencies end up in the output directory so they can be used but the console application does not know about them.

![image](https://user-images.githubusercontent.com/17556515/136617957-a1cb8860-f89e-4043-a1f4-ff3705a5039a.png)
