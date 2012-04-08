import java.io.*;

public class WritableStream extends WorldBinary.IWritableStream
{
    private OutputStream s;
    private long Position;

    public WritableStream(OutputStream s)
    {
        this.s = s;
        this.Position = 0;
    }

    @Override
    public void WriteByte(byte b)
    {
        try
        {
            s.write(b);
            Position += 1;
        }
        catch (IOException e)
        {
            throw new RuntimeException(e);
        }
    }

    @Override
    public void WriteBytes(byte[] l)
    {
        try
        {
            s.write(l);
            Position += l.length;
        }
        catch (IOException e)
        {
            throw new RuntimeException(e);
        }
    }

}
