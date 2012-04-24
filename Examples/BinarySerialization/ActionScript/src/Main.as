//==========================================================================
//
//  File:        Main.as
//  Location:    Yuki.Examples <ActionScript>
//  Description: 数据转换工具
//  Version:     2012.04.24.
//  Author:      F.R.C.
//  Copyright(C) Public Domain
//
//==========================================================================

package
{
    import flash.display.Sprite;
    import flash.filesystem.*;
    import worldBinary.*;

    public class Main extends Sprite
    {
        public function Main():void
        {
            try
            {
                MainInner();
            }
            catch (ex:Error)
            {
                trace(ex);
            }
        }

        public function MainInner():void
        {
            var inf:File = (new File(File.applicationDirectory.nativePath)).resolvePath("..\\..\\SchemaManipulator\\Data\\WorldData.bin");
            var infs:FileStream = new FileStream();
            infs.open(inf, FileMode.READ);
            var w:World = BinaryTranslator.worldFromBinary(infs);
            infs.close();

            var outf:File = (new File(File.applicationDirectory.nativePath)).resolvePath("..\\Data\\WorldData.bin");
            var outfs:FileStream = new FileStream();
            outfs.open(outf, FileMode.WRITE);
            BinaryTranslator.worldToBinary(outfs, w);
            outfs.close();
        }
    }
}