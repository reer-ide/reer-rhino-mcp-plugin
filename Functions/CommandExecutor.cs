using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using Rhino;
using ReerRhinoMCPPlugin.Functions.Commands;

namespace ReerRhinoMCPPlugin.Functions
{
    public class CommandExecutor
    {
        private readonly Dictionary<string, (ICommand commandInstance, MCPCommandAttribute attr)> _commands;

        public CommandExecutor()
        {
            _commands = DiscoverCommands();
            RhinoApp.WriteLine($"CommandExecutor initialized with {_commands.Count} commands.");
        }

        public string ProcessCommand(JObject command, string clientId)
        {
            try
            {
                string commandType = command["type"]?.ToString();
                JObject parameters = command["params"] as JObject ?? new JObject();
                RhinoApp.WriteLine($"Executing command '{commandType}' from client {clientId}");
                JObject result = ExecuteCommand(commandType, parameters);
                return result.ToString();
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"Error processing command: {ex.Message}");
                return CreateErrorResponse(ex.Message).ToString();
            }
        }

        private Dictionary<string, (ICommand commandInstance, MCPCommandAttribute attr)> DiscoverCommands()
        {
            var commands = new Dictionary<string, (ICommand, MCPCommandAttribute)>();
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var commandTypes = assembly.GetTypes()
                    .Where(type => type.IsClass && !type.IsAbstract && typeof(ICommand).IsAssignableFrom(type))
                    .ToList();

                foreach (var commandType in commandTypes)
                {
                    var attr = commandType.GetCustomAttribute<MCPCommandAttribute>();
                    if (attr != null)
                    {
                        var commandInstance = (ICommand)Activator.CreateInstance(commandType);
                        commands[attr.CommandName] = (commandInstance, attr);
                        RhinoApp.WriteLine($"Registered command: {attr.CommandName}");
                    }
                }
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"Error discovering commands: {ex.Message}");
            }
            return commands;
        }

        private JObject ExecuteCommand(string commandType, JObject parameters)
        {
            if (string.IsNullOrEmpty(commandType) || !_commands.TryGetValue(commandType, out var command))
            {
                return CreateErrorResponse($"Unknown command type: {commandType}");
            }

            var (commandInstance, attr) = command;
            if (attr.RequiresDocument && RhinoDoc.ActiveDoc == null)
            {
                return CreateErrorResponse("Command requires an active Rhino document.");
            }

            try
            {
                if (attr.ModifiesDocument && RhinoDoc.ActiveDoc != null)
                {
                    var doc = RhinoDoc.ActiveDoc;
                    var undoRecord = doc.BeginUndoRecord($"MCP Command: {commandType}");
                    var result = commandInstance.Execute(parameters);
                    doc.EndUndoRecord(undoRecord);
                    return CreateSuccessResponse(result);
                }
                else
                {
                    var result = commandInstance.Execute(parameters);
                    return CreateSuccessResponse(result);
                }
            }
            catch (Exception ex)
            {
                return CreateErrorResponse($"Error executing '{commandType}': {ex.Message}");
            }
        }

        private JObject CreateSuccessResponse(JObject result) => new JObject { ["status"] = "success", ["result"] = result };
        private JObject CreateErrorResponse(string message) => new JObject { ["status"] = "error", ["message"] = message };
    }
} 