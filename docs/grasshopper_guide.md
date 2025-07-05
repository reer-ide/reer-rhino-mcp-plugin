# Grasshopper Integration Guide: Finding, Referencing, and Creating Components Through Script

## Overview

The Grasshopper API allows you to programmatically access and manipulate components on the canvas through the `IGH_DocumentObject` interface. All components inherit from this base interface, which provides access to properties like Name, NickName, GUID, and other component data.

## Core Concepts

### Key Interfaces and Classes
- **`IGH_DocumentObject`**: Base interface for all canvas objects
- **`IGH_Component`**: Interface for components with inputs/outputs  
- **`GH_Document`**: The Grasshopper document containing all objects
- **`GH_Component`**: Base class for custom components

### Component Properties
All objects on the canvas have these key members:
- **Name**: Official component name
- **NickName**: User-changeable display name in UI
- **Description**: Brief explanation (visible in tooltips)
- **Category**: Tab name containing the object  
- **SubCategory**: Panel name containing the object
- **ComponentGuid**: Unique identifier for the component type
- **InstanceGuid**: Unique identifier for this specific instance

## Extracting Loaded Components Information

### In C# Script Component
```csharp
// Access all objects in the current document
var libraries = Grasshopper.Instances.ComponentServer.Libraries;
foreach (var library in libraries)
{
    GH_AssemblyInfo libraryInfo = library as GH_AssemblyInfo;
    string libraryName = libraryInfo.Name;
    string libraryAuthor = libraryInfo.AuthorName;
    string libraryDescription = libraryInfo.Description;
    bool libraryIsCore = libraryInfo.IsCoreLibrary;
    List<GH_Component> types = new List<GH_Component>();
    List<string> typeNames = new List<string>();
    List<string> compNames = new List<string>();
    List<string> Cats = new List<string>();
    List<string> subCats = new List<string>();
    List<string> compDescriptions = new List<string>();
    List<string> compGuid = new List<string>();
    var compsInputs = new DataTree<string>();
    var compsOutputs = new DataTree<string>();

    try {
        var componentsInAssembly = library.Assembly.GetTypes().Where(t => t.IsSubclassOf(typeof(GH_Component))).OrderBy(t => t.ToString());

        foreach(Type t in componentsInAssembly){
            int index = 0;
            var path = new GH_Path(index);
            try {
            GH_Component component = Activator.CreateInstance(t) as GH_Component;
            if(component.Exposure != GH_Exposure.hidden){
                types.Add(component);
                typeNames.Add(t.ToString());
                compNames.Add(component.Name);
                compDescriptions.Add(component.Description);
                subCats.Add(component.SubCategory);
                Cats.Add(component.Category);
                compGuid.Add(component.ComponentGuid.ToString());
                List<IGH_Param> compInputs = component.Params.Input;
                List<IGH_Param> compOutputs = component.Params.Output;
                foreach(IGH_Param input in compInputs){
                    compsInputs.Add(input.Name, path);
                    compsInputs.Add(input.Kind.TypeName, path); // Gets a human readable description of the data stored in this parameter.
                    compsInputs.Add(input.Description, path);
                }
                foreach(IGH_Param output in compOutputs){
                    compsOutputs.Add(output.Name, path);
                    compsOutputs.Add(output.Kind.TypeName, path); // Gets a human readable description of the data stored in this parameter.
                    compsOutputs.Add(output.Description, path);
                }
                index++;
            }

            } catch {

            }
      }

    } catch {

    }

}
```
## Accessing the Grasshopper Document

### In C# Script Component
```csharp
// Access all objects in the current document
foreach (IGH_DocumentObject obj in GrasshopperDocument.Objects)
{
    // Work with each object
    Print(obj.NickName);
}
```

### In Visual Studio Plugin
When working in Visual Studio (not script component), you need to access the document differently:
```csharp
// Get the active document
GH_Document doc = Grasshopper.Instances.ActiveCanvas.Document;
foreach (IGH_DocumentObject obj in doc.Objects)
{
    // Work with each object
}
```

## Finding Components by Different Methods

### 1. Find by GUID
Use the document's FindObject method to locate a component by its GUID:
```csharp
// Find object by instance GUID
Guid targetGuid = new Guid("your-guid-here");
IGH_DocumentObject foundObject = GrasshopperDocument.FindObject(targetGuid, false);

if (foundObject != null)
{
    Print($"Found: {foundObject.NickName}");
}
```

### 2. Find by NickName
Iterate through all objects and match by nickname:
```csharp
string targetNickName = "MyComponent";
List<IGH_DocumentObject> foundComponents = new List<IGH_DocumentObject>();

foreach (IGH_DocumentObject obj in GrasshopperDocument.Objects)
{
    if (obj.NickName == targetNickName)
    {
        foundComponents.Add(obj);
    }
}

// With wildcard support (using Contains)
foreach (IGH_DocumentObject obj in GrasshopperDocument.Objects)
{
    if (obj.NickName.Contains("Slider"))
    {
        foundComponents.Add(obj);
    }
}
```

### 3. Find by Component Type
Cast objects to specific types to find components of a particular kind:
```csharp
// Find all number sliders
List<Grasshopper.Kernel.Special.GH_NumberSlider> sliders = 
    new List<Grasshopper.Kernel.Special.GH_NumberSlider>();

foreach (IGH_DocumentObject docObject in GrasshopperDocument.Objects)
{
    var slider = docObject as Grasshopper.Kernel.Special.GH_NumberSlider;
    if (slider != null)
    {
        sliders.Add(slider);
    }
}

// Find all components (not parameters)
List<IGH_Component> components = new List<IGH_Component>();
foreach (IGH_DocumentObject obj in GrasshopperDocument.Objects)
{
    var component = obj as IGH_Component;
    if (component != null)
    {
        components.Add(component);
    }
}
```

### 4. Find by Category/Subcategory
```csharp
// Find components in specific category
foreach (IGH_DocumentObject obj in GrasshopperDocument.Objects)
{
    if (obj.Category == "Curve" && obj.SubCategory == "Primitive")
    {
        Print($"Found curve primitive: {obj.Name}");
    }
}
```

### 5. Find Selected Components
Access only currently selected components:
```csharp
foreach (IGH_DocumentObject obj in GrasshopperDocument.Objects)
{
    if (obj.Attributes.Selected)
    {
        Print($"Selected: {obj.NickName}");
    }
}
```

## Accessing Component Properties and Data

### Basic Component Information
Once you have a component reference, you can access its properties:
```csharp
foreach (IGH_DocumentObject obj in GrasshopperDocument.Objects)
{
    Print($"Name: {obj.Name}");
    Print($"NickName: {obj.NickName}");
    Print($"Description: {obj.Description}");
    Print($"Category: {obj.Category} > {obj.SubCategory}");
    Print($"Component GUID: {obj.ComponentGuid}");
    Print($"Instance GUID: {obj.InstanceGuid}");
}
```

### Accessing Component Inputs and Outputs
For components with parameters, cast to IGH_Component to access inputs/outputs:
```csharp
foreach (IGH_DocumentObject obj in GrasshopperDocument.Objects)
{
    var component = obj as IGH_Component;
    if (component != null)
    {
        Print($"Component: {component.NickName}");
        Print($"Input count: {component.Params.Input.Count}");
        Print($"Output count: {component.Params.Output.Count}");
        
        // Access input parameters
        for (int i = 0; i < component.Params.Input.Count; i++)
        {
            var input = component.Params.Input[i];
            Print($"  Input {i}: {input.NickName}");
            
            // Check what's connected to this input
            if (input.Sources.Count > 0)
            {
                foreach (var source in input.Sources)
                {
                    Print($"    Connected from: {source.Attributes.GetTopLevel.DocObject.NickName}");
                }
            }
        }
    }
}
```

### Accessing Parameter Values
For number sliders and other parameters, you can access their values:
```csharp
foreach (IGH_DocumentObject obj in GrasshopperDocument.Objects)
{
    // Number slider example
    var slider = obj as Grasshopper.Kernel.Special.GH_NumberSlider;
    if (slider != null)
    {
        Print($"Slider '{slider.NickName}' value: {slider.CurrentValue}");
        Print($"Range: {slider.Slider.Minimum} to {slider.Slider.Maximum}");
    }
    
    // Text panel example
    var panel = obj as Grasshopper.Kernel.Special.GH_Panel;
    if (panel != null)
    {
        Print($"Panel '{panel.NickName}' text: {panel.UserText}");
    }
}
```

## Modifying Components

### Changing Component Properties
```csharp
// Find and modify a component
foreach (IGH_DocumentObject obj in GrasshopperDocument.Objects)
{
    if (obj.NickName == "MySlider")
    {
        var slider = obj as Grasshopper.Kernel.Special.GH_NumberSlider;
        if (slider != null)
        {
            // Change slider value
            slider.SetSliderValue((decimal)5.0);
            slider.ExpireSolution(true); // Force recalculation
        }
    }
}
```

### Modifying Panel Text
Example of updating a text panel:
```csharp
Grasshopper.Kernel.Special.GH_Panel panel = null;
foreach (IGH_DocumentObject obj in GrasshopperDocument.Objects)
{
    if (obj.NickName == "Status Panel")
    {
        panel = obj as Grasshopper.Kernel.Special.GH_Panel;
        break;
    }
}

if (panel != null)
{
    panel.UserText = "Updated text";
    panel.ExpireSolution(true);
}
```

## Advanced Techniques

### Using Groups to Organize Components
Find components within specific groups:
```csharp
foreach (IGH_DocumentObject obj in GrasshopperDocument.Objects)
{
    var group = obj as Grasshopper.Kernel.Special.GH_Group;
    if (group != null && group.NickName.StartsWith("MyGroup"))
    {
        // Iterate through objects in this group
        foreach (Guid id in group.ObjectIDs)
        {
            var groupMember = GrasshopperDocument.FindObject(id, false);
            if (groupMember != null)
            {
                Print($"Group member: {groupMember.NickName}");
            }
        }
    }
}
```

### Component Interrogation Function
Here's a comprehensive function to extract all information about a component:
```csharp
public void InterrogateComponent(IGH_DocumentObject obj)
{
    Print($"=== Component Analysis ===");
    Print($"Name: {obj.Name}");
    Print($"NickName: {obj.NickName}");
    Print($"Description: {obj.Description}");
    Print($"Category: {obj.Category} > {obj.SubCategory}");
    Print($"Instance GUID: {obj.InstanceGuid}");
    Print($"Component GUID: {obj.ComponentGuid}");
    Print($"Type: {obj.GetType().Name}");
    
    // Check if it's a component with parameters
    var component = obj as IGH_Component;
    if (component != null)
    {
        Print($"Inputs: {component.Params.Input.Count}");
        Print($"Outputs: {component.Params.Output.Count}");
    }
    
    // Check if it's a parameter
    var param = obj as IGH_Param;
    if (param != null)
    {
        Print($"Data type: {param.TypeName}");
        Print($"Access: {param.Access}");
    }
}

// Usage
foreach (IGH_DocumentObject obj in GrasshopperDocument.Objects)
{
    if (obj.Attributes.Selected)
    {
        InterrogateComponent(obj);
    }
}
```

## Official API Documentation Resources

### Key Documentation Links
- **Main Developer Portal**: https://developer.rhino3d.com/
- **Grasshopper Guides**: https://developer.rhino3d.com/guides/grasshopper/
- **API References**: https://developer.rhino3d.com/api/
- **Grasshopper SDK**: Available through Grasshopper's Help menu

### Documentation Structure
The official documentation is organized into several sections:

1. **Guides**: Step-by-step tutorials for common tasks
   - Creating components and plugins
   - Scripting in Python and C#
   - Data structures and algorithms

2. **API Reference**: Detailed class and method documentation
   - `Grasshopper.Kernel` namespace (core functionality)
   - `Grasshopper.Kernel.Special` (sliders, panels, etc.)
   - `Grasshopper.Kernel.Parameters` (parameter types)

3. **Examples**: Code samples and practical implementations

### How to Look Up Specific Functions

#### Method 1: Direct Namespace Navigation
All Grasshopper classes are in the `Grasshopper.Kernel` namespace:
```
https://developer.rhino3d.com/api/grasshopper/html/N_Grasshopper_Kernel.htm
```

Key classes to explore:
- `IGH_DocumentObject`: Base for all canvas objects
- `GH_Document`: Document container and methods like `FindObject()`
- `IGH_Component`: Components with inputs/outputs
- `GH_ComponentServer`: For finding component types

#### Method 2: Using Search in Documentation
Search for specific class names like:
- "GH_NumberSlider" for slider components
- "GH_Panel" for text panels  
- "FindObject" for locating components

#### Method 3: IntelliSense in IDE
When developing in Visual Studio, IntelliSense will show available methods and properties for any Grasshopper object.

### Practical Example: Looking Up Component Methods
```csharp
// To find methods available on IGH_DocumentObject:
// 1. Go to: developer.rhino3d.com/api/grasshopper/
// 2. Navigate to Grasshopper.Kernel namespace
// 3. Find IGH_DocumentObject interface
// 4. View Methods section

// Common methods you'll find:
obj.NickName           // Get/set display name
obj.InstanceGuid       // Unique instance identifier
obj.ComponentGuid      // Component type identifier  
obj.ExpireSolution()   // Force recalculation
obj.Attributes.Selected // Check if selected
```

## Best Practices

1. **Always Check for Null**: When casting objects, always verify the cast succeeded
2. **Use ExpireSolution()**: After modifying component values, call `ExpireSolution(true)` to trigger recalculation
3. **Handle Threading**: When modifying components from external threads, use `Grasshopper.Instances.ActiveCanvas.BeginInvoke()`
4. **GUID vs NickName**: Use GUIDs for permanent references, NickNames for user-friendly identification
5. **Performance**: Cache component references if you'll access them repeatedly
6. **Documentation**: Always reference the official API docs for the most current method signatures

## Common Use Cases

- **Parameter Studies**: Automatically modify sliders and capture results
- **Component Validation**: Check if required components are present and properly configured  
- **Dynamic UI**: Update panels and other display components based on calculations
- **Workflow Automation**: Batch process multiple components or documents
- **Custom Analytics**: Extract data about component usage and connections

## Learning Path Recommendations

1. **Start with Script Components**: Use the C# or Python script components in Grasshopper to experiment with the API
2. **Explore the SDK Documentation**: Download the Grasshopper SDK help file from the Help menu
3. **Study Existing Components**: Look at open-source Grasshopper plugins on GitHub
4. **Join the Community**: Participate in discussions on the McNeel Developer Forum
5. **Build Simple Tools**: Start with basic component finding and data extraction before attempting complex modifications

This API provides powerful capabilities for creating intelligent, self-modifying Grasshopper definitions and custom workflow tools. The key is understanding the inheritance hierarchy and knowing where to find the documentation for specific functionality.