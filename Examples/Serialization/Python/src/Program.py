#!/usr/bin/python3
#==========================================================================
#
#  File:        Program.py
#  Location:    Niveum.Examples <Python>
#  Description: 数据转换工具
#  Version:     2026.05.30.
#  Author:      F.R.C.
#  Copyright(C) Public Domain
#
#==========================================================================

from World import *
from WorldBinary import *
from WorldJson import *
import sys
import json


def DisplayInfo() -> None:
    print("数据转换工具")
    print("DataConv，Public Domain")
    print("F.R.C.")
    print("")
    print("用法:")
    print("Program.py (/<Command>)*")
    print("将二进制数据转化为JSON数据")
    print("/b2j:<BinaryFile>,<JsonFile>")
    print("将JSON数据转化为二进制数据")
    print("/j2b:<JsonFile>,<BinaryFile>")
    print("复制二进制数据")
    print("/b2b:<BinaryFile1>,<BinaryFile2>")
    print("BinaryFile 二进制文件路径。")
    print("JsonFile JSON文件路径。")
    print("")
    print("示例:")
    print("Program.py /b2j:..\\..\\SchemaManipulator\\Data\\WorldData.bin,..\\Data\\WorldData.json")
    print("将WorldData.bin转化为WorldData.json。")


class ReadableStream(IReadableStream):
    def __init__(self, path: str) -> None:
        self._file = open(path, "rb")

    def ReadByte(self) -> int:
        b = ord(self._file.read(1))
        if b < 0:
            raise IndexError
        return b

    def ReadBytes(self, size: int) -> List[Byte]:
        if size < 0:
            raise IndexError
        l = bytearray(self._file.read(size))
        if len(l) != size:
            raise IndexError
        return l

    def __enter__(self):
        return self

    def __exit__(self, exception_type, exception_value, traceback):
        self._file.close()


class WritableStream(IWritableStream):
    def __init__(self, path: str) -> None:
        self._file = open(path, "wb")

    def WriteByte(self, b: Byte) -> None:
        self._file.write(bytes([b]))

    def WriteBytes(self, Buffer: List[Byte]) -> None:
        l = bytes(Buffer)
        self._file.write(l)

    def __enter__(self):
        return self

    def __exit__(self, exception_type, exception_value, traceback):
        self._file.close()


def BinaryToJson(BinaryPath: str, JsonPath: str) -> None:
    """Read binary data, deserialize, and write as JSON."""
    Data = None
    with open(BinaryPath, "rb") as f:
        Bytes = list(bytearray(f.read()))
    Data = BinaryTranslator.WorldFromBytes(Bytes)
    j = JsonTranslator.WorldToJson(Data)
    with open(JsonPath, "w", encoding="utf-8") as f:
        json.dump(j, f, indent=4, ensure_ascii=False)


def JsonToBinary(JsonPath: str, BinaryPath: str) -> None:
    """Read JSON data, deserialize, and write as binary."""
    with open(JsonPath, "r", encoding="utf-8") as f:
        j = json.load(f)
    Data = JsonTranslator.WorldFromJson(j)
    Bytes = BinaryTranslator.WorldToBytes(Data)
    with open(BinaryPath, "wb") as f:
        f.write(bytes(Bytes))


def BinaryToBinary(BinaryPath1: str, BinaryPath2: str) -> None:
    """Copy binary data."""
    Data = None
    with ReadableStream(BinaryPath1) as rs:
        Data = BinaryTranslator.WorldFromBinary(rs)
    with WritableStream(BinaryPath2) as ws:
        BinaryTranslator.WorldToBinary(ws, Data)


def ParseCommandLine(argv: List[str]) -> List[tuple]:
    """Parse C#-style command line: /option:arg1,arg2"""
    commands = []
    for arg in argv[1:]:
        if arg.startswith("/") or arg.startswith("-"):
            # Remove leading / or -
            arg = arg[1:]
            # Check for help
            if arg.lower() in ("?", "help"):
                return [("help", [])]
            # Split option and arguments
            if ":" in arg:
                opt_name, opt_args_str = arg.split(":", 1)
                opt_args = opt_args_str.split(",")
            else:
                opt_name = arg
                opt_args = []
            commands.append((opt_name.lower(), opt_args))
        else:
            # Positional argument - treat as error or ignore
            pass
    return commands


def main() -> int:
    argv = sys.argv

    if len(argv) == 1:
        DisplayInfo()
        return 0

    commands = ParseCommandLine(argv)

    for cmd_name, cmd_args in commands:
        if cmd_name == "help" or cmd_name == "?":
            DisplayInfo()
            return 0
        elif cmd_name == "b2j":
            if len(cmd_args) == 2:
                BinaryToJson(cmd_args[0], cmd_args[1])
            else:
                DisplayInfo()
                return -1
        elif cmd_name == "j2b":
            if len(cmd_args) == 2:
                JsonToBinary(cmd_args[0], cmd_args[1])
            else:
                DisplayInfo()
                return -1
        elif cmd_name == "b2b":
            if len(cmd_args) == 2:
                BinaryToBinary(cmd_args[0], cmd_args[1])
            else:
                DisplayInfo()
                return -1
        else:
            print(f"Unknown command: {cmd_name}")
            DisplayInfo()
            return -1

    return 0


if __name__ == "__main__":
    sys.exit(main())
