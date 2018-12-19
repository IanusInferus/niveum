import java.io.*;

public class ReadableStream extends world.binary.IReadableStream
{
    private InputStream s;
    private long Position;
    private long FileLength;

    public ReadableStream(InputStream s, long FileLength)
    {
        this.s = s;
        this.Position = 0;
        this.FileLength = FileLength;
    }

    @Override
    public byte ReadByte()
    {
        if (Position + 1 > FileLength)
        {
            throw new RuntimeException(new EOFException());
        }
        try
        {
            int b = s.read();
            if (b == -1)
            {
                throw new RuntimeException(new EOFException());
            }
            Position += 1;
            return (byte) b;
        }
        catch (IOException e)
        {
            throw new RuntimeException(e);
        }
    }

    @Override
    public byte[] ReadBytes(int Size)
    {
        if (Position + Size > FileLength)
        {
            throw new RuntimeException(new EOFException());
        }
        try
        {
            byte[] l = new byte[Size];
            int Count = s.read(l);
            if (Count == -1)
            {
                throw new RuntimeException(new EOFException());
            }
            Position += Count;
            if (Count != Size)
            {
                throw new RuntimeException(new IllegalStateException());
            }
            return l;
        }
        catch (IOException e)
        {
            throw new RuntimeException(e);
        }
    }
}

