import java.io.*;

public class Program
{
    public static void main(String[] args)
    {
        try
        {
        	System.exit(MainInner(args));
        }
        catch (Throwable e)
        {
            e.printStackTrace();
            System.exit(-1);
        }
    }
    
    public static void DisplayInfo()
    {
        System.out.println("数据复制工具");
        System.out.println("DataCopy，Public Domain");
        System.out.println("F.R.C.");
        System.out.println("");
        System.out.println("用法:");
        System.out.println("DataCopy <BinaryFile1> <BinaryFile2>");
        System.out.println("复制二进制数据");
        System.out.println("BinaryFile1 二进制文件路径。");
        System.out.println("BinaryFile2 二进制文件路径。");
        System.out.println("");
        System.out.println("示例:");
        System.out.println("DataCopy ..\\..\\SchemaManipulator\\Data\\WorldData.bin ..\\Data\\WorldData.bin");
        System.out.println("复制WorldData.bin。");
    }
    
    public static int MainInner(String[] argv)
    {
    	if (argv.length != 2)
    	{
    		DisplayInfo();
    		return -1;
    	}
    	BinaryToBinary(argv[0], argv[1]);
    	return 0;
    }
    
    public static void BinaryToBinary(String BinaryPath1, String BinaryPath2)
    {
		try
		{
	        FileInputStream fis = new FileInputStream(BinaryPath1);
	        ReadableStream rs = new ReadableStream(fis, fis.getChannel().size());
	        world.World Data = world.binary.BinaryTranslator.WorldFromBinary(rs);
	        fis.close();

	        FileOutputStream fos = new FileOutputStream(BinaryPath2);
	        WritableStream ws = new WritableStream(fos);
	        world.binary.BinaryTranslator.WorldToBinary(ws, Data);
	        fos.close();
		}
		catch (IOException e)
		{
			throw new RuntimeException(e);
		}
    }
}
