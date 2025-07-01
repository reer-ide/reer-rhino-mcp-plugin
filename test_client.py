#!/usr/bin/env python3
"""
Test client for the RhinoReer MCP Plugin TCP server

Usage:
1. Start Rhino and load the RhinoReer plugin
2. In Rhino command line, type: RhinoReer
3. Enter: local_start
4. Run this test client: python test_client.py

This client connects to the running RhinoReer server and sends test commands.
"""

import socket
import json
import time
import sys

def test_mcp_server(host='127.0.0.1', port=1999):
    """Test the MCP server with various commands"""
    
    print(f"Connecting to RhinoReer MCP server at {host}:{port}...")
    print("Make sure you've started the server in Rhino with: RhinoReer → local_start")
    print()
    
    try:
        # Create socket connection
        sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        sock.settimeout(10)  # 10 second timeout
        sock.connect((host, port))
        
        print("✓ Connected successfully!")
        
        # Test commands
        test_commands = [
            {
                "type": "ping",
                "params": {}
            },
            {
                "type": "get_rhino_info",
                "params": {}
            },
            {
                "type": "get_document_info",
                "params": {}
            },
            {
                "type": "create_cube",
                "params": {
                    "width": 2.0,
                    "length": 3.0,
                    "height": 1.5,
                    "position": [0, 0, 0],
                    "name": "Test Cube 1"
                }
            },
            {
                "type": "create_cube",
                "params": {
                    "width": 1.0,
                    "position": [5, 0, 0],
                    "name": "Small Cube"
                }
            },
            {
                "type": "create_sphere",
                "params": {
                    "radius": 1.5,
                    "center": [0, 8, 0],
                    "name": "Test Sphere 1"
                }
            },
            {
                "type": "create_sphere",
                "params": {
                    "radius": 0.8,
                    "center": [3, 8, 1],
                    "name": "Small Sphere"
                }
            },
            {
                "type": "create_cube",
                "params": {
                    "width": 0.5,
                    "length": 0.5,
                    "height": 2.0,
                    "position": [0, 5, 0]
                }
            },
            {
                "type": "unknown_command",
                "params": {}
            }
        ]
        
        for i, command in enumerate(test_commands, 1):
            print(f"\n--- Test {i}: {command['type']} ---")
            
            # Send command
            command_json = json.dumps(command)
            print(f"Sending: {command_json}")
            
            sock.send(command_json.encode('utf-8'))
            
            # Receive response
            response_data = sock.recv(4096)
            response_text = response_data.decode('utf-8')
            
            print(f"Received: {response_text}")
            
            try:
                response_obj = json.loads(response_text)
                print(f"Status: {response_obj.get('status', 'unknown')}")
                if 'result' in response_obj:
                    result = response_obj['result']
                    print(f"Result: {json.dumps(result, indent=2)}")
                    
                    # Special handling for geometry creation results
                    if command['type'] in ['create_cube', 'create_sphere'] and response_obj.get('status') == 'success':
                        print(f"  ✓ Created object: {result.get('name', 'Unnamed')} (ID: {result.get('object_id', 'Unknown')})")
                        
                        if command['type'] == 'create_cube' and 'dimensions' in result:
                            dims = result['dimensions']
                            print(f"  ✓ Dimensions: {dims.get('width')}×{dims.get('length')}×{dims.get('height')}")
                        elif command['type'] == 'create_sphere' and 'radius' in result:
                            print(f"  ✓ Radius: {result.get('radius')}")
                            
                        if 'position' in result:
                            pos = result['position']
                            print(f"  ✓ Position: ({pos[0]}, {pos[1]}, {pos[2]})")
                        elif 'center' in result:
                            center = result['center']
                            print(f"  ✓ Center: ({center[0]}, {center[1]}, {center[2]})")
                            
                if 'message' in response_obj:
                    print(f"Message: {response_obj['message']}")
            except json.JSONDecodeError:
                print("Response is not valid JSON")
            
            # Small delay between commands
            time.sleep(0.5)
        
        print("\n--- All tests completed ---")
        print("\nNote: If geometry creation tests succeeded, you should see new objects in your Rhino viewport!")
        print("  - Cubes at various positions")
        print("  - Spheres along the Y=8 line")
        
    except ConnectionRefusedError:
        print(f"✗ ERROR: Could not connect to server at {host}:{port}")
        print("Make sure you have:")
        print("  1. Rhino running with RhinoReer plugin loaded")
        print("  2. Started the server with: RhinoReer → local_start")
        return False
    except socket.timeout:
        print("ERROR: Connection timed out")
        return False
    except Exception as e:
        print(f"ERROR: {e}")
        return False
    finally:
        try:
            sock.close()
        except:
            pass
    
    return True

def test_single_command(command_type, params=None, host='127.0.0.1', port=1999):
    """Test a single command"""
    if params is None:
        params = {}
    
    print(f"Testing single command: {command_type}")
    
    try:
        sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        sock.settimeout(10)
        sock.connect((host, port))
        
        command = {
            "type": command_type,
            "params": params
        }
        
        command_json = json.dumps(command)
        print(f"Sending: {command_json}")
        
        sock.send(command_json.encode('utf-8'))
        response_data = sock.recv(4096)
        response_text = response_data.decode('utf-8')
        
        print(f"Response: {response_text}")
        
        sock.close()
        return True
        
    except Exception as e:
        print(f"Error: {e}")
        return False

if __name__ == "__main__":
    # Parse command line arguments
    if len(sys.argv) > 1 and sys.argv[1] == "single":
        # Single command mode
        if len(sys.argv) < 3:
            print("Usage: python test_client.py single <command_type> [host] [port]")
            print("Example: python test_client.py single create_cube")
            print("Example: python test_client.py single create_sphere")
            sys.exit(1)
        
        command_type = sys.argv[2]
        host = sys.argv[3] if len(sys.argv) > 3 else '127.0.0.1'
        port = int(sys.argv[4]) if len(sys.argv) > 4 else 1999
        
        # Example parameters for different commands
        params = {}
        if command_type == "create_cube":
            params = {
                "width": 2.0,
                "length": 1.5,
                "height": 1.0,
                "position": [10, 0, 0],
                "name": "CLI Test Cube"
            }
        elif command_type == "create_sphere":
            params = {
                "radius": 1.2,
                "center": [10, 5, 0],
                "name": "CLI Test Sphere"
            }
        
        success = test_single_command(command_type, params, host, port)
        sys.exit(0 if success else 1)
    else:
        # Full test mode
        host = sys.argv[1] if len(sys.argv) > 1 else '127.0.0.1'
        port = int(sys.argv[2]) if len(sys.argv) > 2 else 1999
        
        print("RhinoReer MCP Plugin Test Client")
        print("=" * 40)
        print("Testing modular command architecture:")
        print("  - Individual command classes")
        print("  - Automatic command discovery")
        print("  - Easy extensibility")
        print()
        
        success = test_mcp_server(host, port)
        
        if success:
            print("\nTest completed successfully!")
            sys.exit(0)
        else:
            print("\nTest failed!")
            sys.exit(1) 