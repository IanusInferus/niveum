import java.io.*;

public class ReadableStream extends WorldBinary.IReadableStream
{
    private InputStream s;
    private long Position;

    public ReadableStream(InputStream s)
    {
        this.s = s;
        this.Position = 0;
    }

    @Override
    public byte ReadByte()
    {
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

