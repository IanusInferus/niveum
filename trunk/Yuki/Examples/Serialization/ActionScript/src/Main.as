//==========================================================================
//
//  File:        Main.as
//  Location:    Yuki.Examples <ActionScript>
//  Description: 数据转换工具
//  Version:     2013.03.31.
//  Author:      F.R.C.
//  Copyright(C) Public Domain
//
//==========================================================================

package
{
    import flash.display.Sprite;
    import flash.filesystem.*;
    import world.*;

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

        public function CopyBinary():void
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

        public function CopyJson():void
        {
            var inf:File = (new File(File.applicationDirectory.nativePath)).resolvePath("..\\..\\CSharp\\Data\\WorldData.json");
            var infs:FileStream = new FileStream();
            infs.open(inf, FileMode.READ);
            var t:String = infs.readUTFBytes(inf.size);
            var j:Object = JSON.parse(t);
            var w:World = JsonTranslator.worldFromJson(j);
            infs.close();

            var outf:File = (new File(File.applicationDirectory.nativePath)).resolvePath("..\\Data\\WorldData.json");
            var outfs:FileStream = new FileStream();
            outfs.open(outf, FileMode.WRITE);
            var j2:Object = JsonTranslator.worldToJson(w);
            var t2:String = JSON.stringify(j2);
            outfs.writeUTFBytes(t2);
            outfs.close();
        }

        public function MainInner():void
        {
            CopyBinary();
            CopyJson();
        }
    }
}
