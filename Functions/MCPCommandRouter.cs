using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using Rhino;
using ReerRhinoMCPPlugin.Core.Common;
using ReerRhinoMCPPlugin.Functions.Commands;

namespace ReerRhinoMCPPlugin.Functions
{
    /// <summary>
    /// Advanced command handler with reflection-based routing for MCP protocol commands
    /// </summary>
    public class MCPCommandRouter
    {
        private readonly Dictionary<string, (ICommand commandInstance, MCPCommandAttribute attr)> _commands;

        public MCPCommandRouter()
        {
            _commands = DiscoverCommands();
            
            RhinoApp.WriteLine($"MCP Command Handler initialized with {_commands.Count} commands");
        }

        /// <summary>
        /// Processes an MCP command and returns a response
        /// </summary>
        /// <param name="command">The command to process</param>
        /// <param name="clientId">ID of the client that sent the command</param>
        /// <returns>JSON response string</returns>
        public string ProcessCommand(JObject command, string clientId)
        {
            try
            {
                string commandType = command["type"]?.ToString();
                JObject parameters = command["params"] as JObject ?? new JObject();

                RhinoApp.WriteLine($"Processing command '{commandType}' from client {clientId}");

                JObject result = ExecuteCommand(commandType, parameters);

                return result.ToString();
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"Error processing command: {ex.Message}");
                return CreateErrorResponse(ex.Message).ToString();
            }
        }

        /// <summary>
        /// Discovers all classes that implement ICommand using reflection
        /// </summary>
        private Dictionary<string, (ICommand commandInstance, MCPCommandAttribute attr)> DiscoverCommands()
        {
            var commands = new Dictionary<string, (ICommand, MCPCommandAttribute)>();

            try
            {
                // Get all types that implement ICommand
                var assembly = Assembly.GetExecutingAssembly();
                var commandTypes = assembly.GetTypes()
                    .Where(type => type.IsClass && !type.IsAbstract && typeof(ICommand).IsAssignableFrom(type))
                    .ToList();

                foreach (var commandType in commandTypes)
                {
                    try
                    {
                        // Get the MCPCommand attribute
                        var attr = commandType.GetCustomAttribute<MCPCommandAttribute>();
                        if (attr != null)
                        {
                            // Create an instance of the command
                            var commandInstance = (ICommand)Activator.CreateInstance(commandType);
                            
                            commands[attr.CommandName] = (commandInstance, attr);
                            RhinoApp.WriteLine($"Registered command: {attr.CommandName} - {attr.Description} ({commandType.Name})");
                        }
                        else
                        {
                            RhinoApp.WriteLine($"Warning: Command class {commandType.Name} implements ICommand but lacks MCPCommandAttribute");
                        }
                    }
                    catch (Exception ex)
                    {
                        RhinoApp.WriteLine($"Error creating instance of {commandType.Name}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"Error discovering commands: {ex.Message}");
            }

            return commands;
        }

        /// <summary>
        /// Executes a command using the reflection-based routing system
        /// </summary>
        private JObject ExecuteCommand(string commandType, JObject parameters)
        {
            if (string.IsNullOrEmpty(commandType))
            {
                return CreateErrorResponse("Command type cannot be empty");
            }

            if (!_commands.TryGetValue(commandType, out var command))
            {
                return CreateErrorResponse($"Unknown command type: {commandType}");
            }

            var (commandInstance, attr) = command;

            // Check if command requires an active document
            if (attr.RequiresDocument && RhinoDoc.ActiveDoc == null)
            {
                return CreateErrorResponse("Command requires an active Rhino document");
            }

            try
            {
                // Handle undo recording for commands that modify the document
                if (attr.ModifiesDocument && RhinoDoc.ActiveDoc != null)
                {
                    var doc = RhinoDoc.ActiveDoc;
                    var undoRecord = doc.BeginUndoRecord($"MCP Command: {commandType}");
                    
                    try
                    {
                        var result = commandInstance.Execute(parameters);
                        return CreateSuccessResponse(result);
                    }
                    finally
                    {
                        doc.EndUndoRecord(undoRecord);
                    }
                }
                else
                {
                    // Execute without undo recording
                    var result = commandInstance.Execute(parameters);
                    return CreateSuccessResponse(result);
                }
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"Error executing command '{commandType}': {ex.Message}");
                return CreateErrorResponse(ex.Message);
            }
        }

        /// <summary>
        /// Creates a success response with the given result
        /// </summary>
        private JObject CreateSuccessResponse(JObject result)
        {
            return new JObject
            {
                ["status"] = "success",
                ["result"] = result
            };
        }

        /// <summary>
        /// Creates an error response with the given message
        /// </summary>
        private JObject CreateErrorResponse(string message)
        {
            return new JObject
            {
                ["status"] = "error",
                ["message"] = message
            };
        }

        /// <summary>
        /// Gets information about all available commands
        /// </summary>
        public JObject GetAvailableCommands()
        {
            var commandsInfo = new JArray();

            foreach (var kvp in _commands)
            {
                var commandName = kvp.Key;
                var (commandInstance, attr) = kvp.Value;

                commandsInfo.Add(new JObject
                {
                    ["name"] = commandName,
                    ["description"] = attr.Description,
                    ["requires_document"] = attr.RequiresDocument,
                    ["modifies_document"] = attr.ModifiesDocument,
                    ["class_name"] = commandInstance.GetType().Name
                });
            }

            return new JObject
            {
                ["commands"] = commandsInfo,
                ["total_count"] = commandsInfo.Count
            };
        }
    }
} 