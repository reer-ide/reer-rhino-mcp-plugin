#!/usr/bin/env python3
"""
Debug script to check what sessions exist on the server for your license/user.
Run this to see all sessions and their details.
"""

import requests
import json
import sys

def debug_sessions(base_url="http://127.0.0.1:8080"):
    """Debug sessions on the remote server"""
    
    # Get your user credentials - you'll need to update these
    # You can find these values in your license registration
    user_id = input("Enter your user_id: ").strip()
    license_id = input("Enter your license_id: ").strip()
    
    if not user_id or not license_id:
        print("User ID and License ID are required")
        return
    
    print(f"\n=== Debugging Sessions ===")
    print(f"Server: {base_url}")
    print(f"User ID: {user_id}")
    print(f"License ID: {license_id}")
    print(f"Target file: C:\\Users\\Zhish\\OneDrive\\GitHub\\reer-rhino-mcp-plugin\\tests\\test_multi_storey.3dm")
    
    try:
        # 1. Get all user sessions
        print(f"\n=== 1. All User Sessions ===")
        response = requests.get(f"{base_url}/sessions/{user_id}")
        if response.status_code == 200:
            data = response.json()
            sessions = data.get("valid_sessions", [])
            print(f"Found {len(sessions)} sessions:")
            for i, session in enumerate(sessions):
                print(f"  Session {i+1}:")
                print(f"    ID: {session['session_id']}")
                print(f"    File: {session['file_path']}")
                print(f"    Status: {session['status']}")
                print(f"    License: {session['license_id']}")
                print(f"    Created: {session['created_at']}")
                print(f"    Port: {session['websocket_port']}")
                print()
        else:
            print(f"Error getting user sessions: {response.status_code}")
            print(response.text)
            
        # 2. Try connecting by file path only
        print(f"=== 2. Test Connection by File Path ===")
        connect_data = {
            "user_id": user_id,
            "license_id": license_id,
            "file_path": "C:\\Users\\Zhish\\OneDrive\\GitHub\\reer-rhino-mcp-plugin\\tests\\test_multi_storey.3dm"
        }
        response = requests.post(f"{base_url}/sessions/connect", json=connect_data)
        print(f"Connection attempt status: {response.status_code}")
        if response.status_code == 200:
            result = response.json()
            print(f"✓ Connection successful!")
            print(f"  Session ID: {result['session_id']}")
            print(f"  Document GUID: {result['document_guid']}")
            print(f"  WebSocket URL: {result['websocket_url']}")
        else:
            print(f"✗ Connection failed:")
            print(response.text)
            
        # 3. Test with normalized paths
        print(f"=== 3. Test with Normalized Path ===")
        connect_data["file_path"] = "C:/Users/Zhish/OneDrive/GitHub/reer-rhino-mcp-plugin/tests/test_multi_storey.3dm"
        response = requests.post(f"{base_url}/sessions/connect", json=connect_data)
        print(f"Normalized path connection status: {response.status_code}")
        if response.status_code == 200:
            result = response.json()
            print(f"✓ Normalized path connection successful!")
        else:
            print(f"✗ Normalized path connection failed:")
            print(response.text)
            
    except requests.exceptions.ConnectionError:
        print(f"Error: Could not connect to server at {base_url}")
        print("Make sure the server is running")
    except Exception as e:
        print(f"Error: {e}")

if __name__ == "__main__":
    debug_sessions()