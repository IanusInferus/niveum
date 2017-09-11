#!/usr/bin/python3

from World import *
from WorldBinary import *
import sys

def DisplayInfo() -> None:
    print("数据复制工具")
    print("DataCopy，Public Domain")
    print("F.R.C.")
    print("")
    print("用法:")
    print("DataCopy <BinaryFile1> <BinaryFile2>")
    print("复制二进制数据")
    print("BinaryFile1 二进制文件路径。")
    print("BinaryFile2 二进制文件路径。")
    print("")
    print("示例:")
    print(".\\Program.py ..\\..\\SchemaManipulator\\Data\\WorldData.bin ..\\Data\\WorldData.bin")
    print("复制WorldData.bin。")

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

def BinaryToBinary(BinaryPath1: str, BinaryPath2: str) -> None:
    Data = None
    with ReadableStream(BinaryPath1) as rs:
        Data = BinaryTranslator.WorldFromBinary(rs)
    with WritableStream(BinaryPath2) as ws:
        BinaryTranslator.WorldToBinary(ws, Data)

argv = sys.argv
if len(argv) != 3:
    DisplayInfo()
    sys.exit(-1)
else:
    BinaryToBinary(argv[1], argv[2])
    sys.exit(0)
