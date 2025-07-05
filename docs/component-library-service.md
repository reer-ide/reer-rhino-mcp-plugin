# Component Library Service Implementation Guide

## Overview

The Component Library Service is a core feature of the REER Rhino MCP Plugin that enables AI systems to discover, understand, and utilize Grasshopper components. This service maintains a local cache of component metadata and provides intelligent search capabilities for AI-assisted workflow creation.

## Architecture

### Core Components

1. **ComponentLibraryService**: Main service class that orchestrates scanning, storage, and search
2. **ComponentScanner**: Handles the actual scanning of Grasshopper libraries
3. **ComponentStorage**: Manages JSON serialization and local file storage
4. **ComponentSearchEngine**: Provides intelligent search and ranking capabilities
5. **MCP Tools**: Exposes functionality through MCP protocol

### Data Flow

```
Plugin Initialization
↓
Check for library changes (signature comparison)
↓
Scan libraries if needed (background process)
↓
Store/update component metadata in JSON cache
↓
AI requests component lookup via MCP
↓
Search engine returns ranked results
↓
AI selects component and requests creation
↓
Plugin creates component on Grasshopper canvas
```

## Implementation Details

### 1. Library Scanning Strategy

#### Scanning Triggers
- **First Launch**: Complete scan of all available libraries
- **Startup Check**: Compare library signature with cached version
- **Manual Refresh**: User-initiated rescan (future enhancement)

#### Scanning Process
```csharp
// Pseudo-code for scanning process
public void ScanLibraries()
{
    var libraries = Grasshopper.Instances.ComponentServer.Libraries;
    var componentData = new ComponentLibraryData();
    
    foreach (var library in libraries)
    {
        var libraryInfo = ExtractLibraryInfo(library);
        var components = ExtractComponents(library);
        
        libraryInfo.Components = components;
        componentData.Libraries.Add(libraryInfo);
    }
    
    SaveToCache(componentData);
}
```

#### Component Metadata Extraction
For each component, extract:
- **Basic Info**: Name, nickname, description, GUID
- **Categorization**: Category, subcategory, library source
- **Interface**: Input parameters, output parameters
- **Metadata**: Author, version, core vs. plugin status

### 2. Storage Format

#### JSON Structure
```json
{
  "version": "1.0",
  "scan_date": "2024-01-15T10:30:00Z",
  "library_signature": "sha256-hash-of-libraries",
  "rhino_version": "8.0.23304.13001",
  "grasshopper_version": "1.0.0007",
  "component_count": 847,
  "libraries": [
    {
      "name": "Grasshopper",
      "author": "Robert McNeel & Associates",
      "is_core": true,
      "version": "1.0.0007",
      "component_count": 423,
      "components": [
        {
          "name": "Box",
          "nickname": "Box",
          "component_guid": "1fe2f727-4f3e-4b21-9e5c-b2c6d2b5f6a3",
          "category": "Surface",
          "subcategory": "Primitive",
          "description": "Create a box aligned to the world axes",
          "inputs": [
            {
              "name": "Base",
              "nickname": "B",
              "description": "Base plane for box",
              "type": "Plane",
              "access": "item",
              "optional": false
            },
            {
              "name": "X",
              "nickname": "X",
              "description": "Size of box in X direction",
              "type": "Number",
              "access": "item",
              "optional": false
            }
          ],
          "outputs": [
            {
              "name": "Box",
              "nickname": "B",
              "description": "Resulting box",
              "type": "Box",
              "access": "item"
            }
          ]
        }
      ]
    }
  ]
}
```

#### Storage Location
- **Path**: `%APPDATA%/McNeel/Rhinoceros/8.0/Plug-ins/ReerRhinoMCP/component_library.json`
- **Backup**: Keep previous version as `component_library.backup.json`
- **Versioning**: Include schema version for future compatibility

### 3. Update Detection

#### Library Signature Generation
```csharp
// Pseudo-code for signature generation
public string GenerateLibrarySignature()
{
    var libraries = Grasshopper.Instances.ComponentServer.Libraries;
    var signatureData = new StringBuilder();
    
    foreach (var library in libraries.OrderBy(l => l.Name))
    {
        signatureData.AppendLine($"{library.Name}|{library.Version}|{library.ComponentCount}");
    }
    
    return ComputeSHA256Hash(signatureData.ToString());
}
```

#### Change Detection Logic
1. Generate current library signature
2. Compare with cached signature
3. If different, trigger incremental or full rescan
4. Update signature after successful scan

### 4. Search Engine

#### Search Strategies
1. **Exact Match**: Direct name/nickname matching (highest priority)
2. **Partial Match**: Substring matching in name/description
3. **Category Match**: Filter by category/subcategory
4. **Keyword Match**: Multiple keyword support with AND/OR logic
5. **Fuzzy Match**: Levenshtein distance for typo tolerance

#### Ranking Algorithm
```csharp
// Pseudo-code for component ranking
public double CalculateRelevanceScore(ComponentInfo component, SearchQuery query)
{
    double score = 0;
    
    // Exact name match (highest weight)
    if (component.Name.Equals(query.MainKeyword, StringComparison.OrdinalIgnoreCase))
        score += 1.0;
    
    // Partial name match
    if (component.Name.Contains(query.MainKeyword, StringComparison.OrdinalIgnoreCase))
        score += 0.8;
    
    // Description relevance
    score += CalculateDescriptionMatch(component.Description, query.Keywords) * 0.6;
    
    // Category match
    if (query.Category != null && component.Category.Equals(query.Category, StringComparison.OrdinalIgnoreCase))
        score += 0.5;
    
    // Usage frequency (future enhancement)
    score += component.UsageCount * 0.1;
    
    return score;
}
```

### 5. MCP Tool Integration

#### LookupGHComponentsTool
```csharp
// Tool for AI component discovery
public class LookupGHComponentsTool : IMCPTool
{
    public string Name => "look_up_gh_components";
    public string Description => "Search for Grasshopper components by keywords and criteria";
    
    public MCPToolSchema Schema => new MCPToolSchema
    {
        Properties = new Dictionary<string, object>
        {
            ["keywords"] = new { type = "array", items = new { type = "string" } },
            ["category"] = new { type = "string", optional = true },
            ["max_results"] = new { type = "integer", default = 10 }
        }
    };
}
```

#### CreateGHComponentTool
```csharp
// Tool for AI component creation
public class CreateGHComponentTool : IMCPTool
{
    public string Name => "create_gh_component";
    public string Description => "Create a Grasshopper component with specified parameters";
    
    public MCPToolSchema Schema => new MCPToolSchema
    {
        Properties = new Dictionary<string, object>
        {
            ["component_name"] = new { type = "string" },
            ["component_guid"] = new { type = "string" },
            ["parameters"] = new { type = "object" },
            ["position"] = new { type = "object", optional = true }
        }
    };
}
```

## Performance Considerations

### Optimization Strategies
1. **Lazy Loading**: Load component details only when needed
2. **Caching**: Keep frequently accessed components in memory
3. **Background Processing**: Perform scans on background threads
4. **Incremental Updates**: Only rescan changed libraries
5. **Search Indexing**: Pre-build search indexes for faster queries

### Memory Management
- **Component Disposal**: Properly dispose instantiated components after scanning
- **Cache Limits**: Implement LRU cache with size limits
- **Garbage Collection**: Minimize object creation during searches

### Startup Performance
- **Deferred Initialization**: Start component service after plugin initialization
- **Progress Reporting**: Show progress during long scans
- **Cancellation Support**: Allow cancellation of long-running operations

## Error Handling

### Robust Error Recovery
1. **Component Instantiation Failures**: Continue scanning other components
2. **Library Loading Errors**: Skip problematic libraries, log warnings
3. **Storage Corruption**: Rebuild cache from scratch
4. **Missing Dependencies**: Handle missing referenced assemblies

### Logging Strategy
```csharp
// Comprehensive logging for debugging
public void LogScanResults(ScanResult result)
{
    RhinoApp.WriteLine($"Component Library Scan Complete:");
    RhinoApp.WriteLine($"  Libraries scanned: {result.LibraryCount}");
    RhinoApp.WriteLine($"  Components found: {result.ComponentCount}");
    RhinoApp.WriteLine($"  Errors encountered: {result.ErrorCount}");
    RhinoApp.WriteLine($"  Scan duration: {result.Duration.TotalSeconds:F2}s");
}
```

## Testing Strategy

### Unit Tests
- Component scanning logic
- Search algorithm accuracy
- JSON serialization/deserialization
- Signature generation and comparison

### Integration Tests
- End-to-end component discovery flow
- MCP tool integration
- Performance with large component libraries
- Error handling scenarios

### Manual Testing
- Various Grasshopper plugin configurations
- Component creation in different contexts
- Search accuracy with real-world queries
- Performance with 500+ components

## Future Enhancements

### Beta Launch Scope
Focus on core functionality:
- Basic component scanning and storage
- Simple keyword search
- Essential MCP tools
- Error handling and logging

### Post-Beta Features
- Advanced search with filters
- Component usage analytics
- Custom component templates
- Component recommendation engine
- Real-time library monitoring 