#!/usr/bin/python3
#==========================================================================
#
#  File:        Client.py
#  Location:    Niveum.Examples <Python>
#  Description: 聊天客户端(Python JSON)
#  Version:     2026.05.31.
#  Author:      F.R.C.
#  Copyright(C) Public Domain
#
#==========================================================================

from __future__ import annotations
from typing import Any, Callable, Optional
import sys
import socket
import json
import re
import threading
from abc import ABC, abstractmethod

from Communication import *
from CommunicationJson import *


def display_title() -> None:
    print("聊天客户端")
    print("Author:      F.R.C.")
    print("Copyright(C) Public Domain")


def display_info() -> None:
    print("用法:")
    print("  python Client.py [<IpAddress=127.0.0.1>] [<Port=8002>]")


# Regex for parsing server responses: /svr CommandName@CommandHash Parameters
_r_svr = re.compile(r'^/svr\s+(?P<Name>\S+)(?:\s+(?P<Params>.*))?$')
_r_name = re.compile(r'^(?P<CommandName>.*?)@(?P<CommandHash>.*)$')


class TcpJsonSender(IJsonSender):
    """TCP transport using line-delimited JSON protocol."""

    def __init__(self, host: str, port: int) -> None:
        self._host = host
        self._port = port
        self._sock: Optional[socket.socket] = None
        self._lock = threading.Lock()
        self._recv_buffer: bytes = b''

    def connect(self) -> None:
        self._sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        self._sock.connect((self._host, self._port))

    def close(self) -> None:
        if self._sock is not None:
            self._sock.close()
            self._sock = None

    def send(self, command_name: str, command_hash: str, parameters: str) -> None:
        """Send a line: /<CommandName>@<Hash> <Parameters>\r\n"""
        line = f"/{command_name}@{command_hash} {parameters}\r\n"
        data = line.encode('utf-8')
        with self._lock:
            if self._sock is None:
                raise RuntimeError("Not connected")
            self._sock.sendall(data)

    def receive(self) -> tuple[str, str, str] | None:
        """Receive a line, parse /svr prefix. Returns (command_name, command_hash, parameters) or None."""
        if self._sock is None:
            raise RuntimeError("Not connected")
        while True:
            # Check if we already have a complete line in the buffer
            newline_pos = self._recv_buffer.find(b'\n')
            if newline_pos >= 0:
                line_bytes = self._recv_buffer[:newline_pos].replace(b'\r', b'')
                self._recv_buffer = self._recv_buffer[newline_pos + 1:]
                line = line_bytes.decode('utf-8')
                return self._parse_line(line)
            # Read more data
            try:
                chunk = self._sock.recv(4096)
            except Exception:
                raise ConnectionError("Connection closed")
            if not chunk:
                raise ConnectionError("Connection closed")
            self._recv_buffer += chunk

    def _parse_line(self, line: str) -> tuple[str, str, str] | None:
        """Parse a server line: /svr CommandName@CommandHash Parameters"""
        if not line:
            return None
        m = _r_svr.match(line)
        if not m:
            return None
        name = m.group('Name')
        params = m.group('Params') or '{}'
        mn = _r_name.match(name)
        if not mn:
            return None
        command_name = mn.group('CommandName')
        command_hash = mn.group('CommandHash')
        return (command_name, command_hash, params)


class InteractiveClient:
    """Interactive chat client using the generated JSON communication types."""

    def __init__(self, host: str, port: int) -> None:
        self._host = host
        self._port = port
        self._sender: Optional[TcpJsonSender] = None
        self._client: Optional[JsonSerializationClient] = None
        self._stop_event = threading.Event()

    def run(self) -> None:
        display_title()

        self._sender = TcpJsonSender(self._host, self._port)
        self._client = JsonSerializationClient(self._sender)

        # Subscribe to server events
        self._client.MessageReceived = self._on_message_received
        self._client.Error = self._on_error
        self._client.ErrorCommand = self._on_error_command
        self._client.ServerShutdown = self._on_server_shutdown

        try:
            self._sender.connect()
            print(f"Connected to {self._host}:{self._port}")
        except Exception as e:
            print(f"Connection failed: {e}")
            return

        # Start receive thread
        receive_thread = threading.Thread(target=self._receive_loop, daemon=True)
        receive_thread.start()

        # Check schema version
        try:
            self._check_schema()
        except Exception as e:
            print(f"Schema check failed: {e}")
            self._sender.close()
            return

        # Interactive loop
        print("Commands: <message> | login | exit | shutdown")
        try:
            while not self._stop_event.is_set():
                try:
                    line = input()
                except EOFError:
                    break

                if not line:
                    continue

                if line == "exit":
                    self._send_quit()
                    break
                elif line == "shutdown":
                    self._send_shutdown()
                    break
                else:
                    self._send_message(line)

        except KeyboardInterrupt:
            pass
        finally:
            self._stop_event.set()
            if self._sender is not None:
                self._sender.close()
            print("Disconnected.")

    def _check_schema(self) -> None:
        request = CheckSchemaVersionRequest(Hash=self._client.hash)
        self._client.CheckSchemaVersion(request, self._on_check_schema_reply)

    def _on_check_schema_reply(self, reply: CheckSchemaVersionReply) -> None:
        if reply.OnNotSupported():
            print("Warning: Client version may not be supported by server.")
        elif reply.OnSupported():
            print("Schema version check: supported by server.")
        else:
            print("Schema version check: up to date.")

    def _send_message(self, content: str) -> None:
        request = SendMessageRequest(Content=content)
        self._client.SendMessage(request,
            callback=lambda r: self._on_send_message_reply(r),
            on_error=lambda e: print(f"SendMessage error: {e}"))

    def _on_send_message_reply(self, reply: SendMessageReply) -> None:
        if reply.OnTooLong():
            print("Message too long.")

    def _send_quit(self) -> None:
        request = QuitRequest()
        self._client.Quit(request,
            callback=lambda r: None,
            on_error=lambda e: print(f"Quit error: {e}"))

    def _send_shutdown(self) -> None:
        request = ShutdownRequest()
        self._client.Shutdown(request,
            callback=lambda r: print("Server is shutting down."),
            on_error=lambda e: print(f"Shutdown error: {e}"))

    def _on_message_received(self, event: MessageReceivedEvent) -> None:
        print(f"[Broadcast] {event.Content}")

    def _on_error(self, event: ErrorEvent) -> None:
        print(f"[Error] {event.Message}")

    def _on_error_command(self, event: ErrorCommandEvent) -> None:
        print(f"[ErrorCommand] {event.CommandName}: {event.Message}")

    def _on_server_shutdown(self, event: ServerShutdownEvent) -> None:
        print("[Server] Server is shutting down.")
        self._stop_event.set()

    def _receive_loop(self) -> None:
        """Background thread that receives lines and dispatches them."""
        while not self._stop_event.is_set():
            try:
                result = self._sender.receive()
                if result is None:
                    continue
                command_name, command_hash, parameters = result
                if self._client is not None:
                    self._client.handle_result(command_name, command_hash, parameters)
            except (ConnectionError, OSError):
                if not self._stop_event.is_set():
                    print("Connection lost.")
                    self._stop_event.set()
                break
            except Exception as e:
                if not self._stop_event.is_set():
                    print(f"Receive error: {e}")


def main() -> int:
    argv = sys.argv

    host = "127.0.0.1"
    port = 8002

    if len(argv) >= 2:
        host = argv[1]
    if len(argv) >= 3:
        try:
            port = int(argv[2])
        except ValueError:
            display_info()
            return -1

    if len(argv) >= 2 and (argv[1] == "?" or argv[1] == "help" or argv[1] == "--help"):
        display_info()
        return 0

    client = InteractiveClient(host, port)
    client.run()
    return 0


if __name__ == "__main__":
    sys.exit(main())
