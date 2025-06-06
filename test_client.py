#!/usr/bin/env python3
"""
Simple test client for the Rhino MCP Plugin TCP server
"""

import socket
import json
import time
import sys

def test_mcp_server(host='127.0.0.1', port=1999):
    """Test the MCP server with various commands"""
    
    print(f"Connecting to Rhino MCP server at {host}:{port}...")
    
    try:
        # Create socket connection
        sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        sock.settimeout(10)  # 10 second timeout
        sock.connect((host, port))
        
        print("Connected successfully!")
        
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
                    print(f"Result: {json.dumps(response_obj['result'], indent=2)}")
                if 'message' in response_obj:
                    print(f"Message: {response_obj['message']}")
            except json.JSONDecodeError:
                print("Response is not valid JSON")
            
            # Small delay between commands
            time.sleep(0.5)
        
        print("\n--- All tests completed ---")
        
    except ConnectionRefusedError:
        print(f"ERROR: Could not connect to server at {host}:{port}")
        print("Make sure the Rhino MCP Plugin is loaded and the server is running.")
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

if __name__ == "__main__":
    # Parse command line arguments
    host = sys.argv[1] if len(sys.argv) > 1 else '127.0.0.1'
    port = int(sys.argv[2]) if len(sys.argv) > 2 else 1999
    
    print("Rhino MCP Plugin Test Client")
    print("=" * 40)
    
    success = test_mcp_server(host, port)
    
    if success:
        print("\nTest completed successfully!")
        sys.exit(0)
    else:
        print("\nTest failed!")
        sys.exit(1) 