import haxe.Json;
import haxe.io.Bytes;
import sys.io.File;

import world.*;
import world.json.*;

using Lambda;

class Main
{
    static function readUtf8File(path : String) : String
    {
        var b = File.getBytes(path);
        var start = 0;
        if (b.length >= 3)
        {
            if ((b.get(0) == 0xEF) && (b.get(1) == 0xBB) && (b.get(2) == 0xBF))
            {
                start = 3;
            }
        }
        return b.getString(start, b.length - start);
    }
    
    static function writeUtf8File(path : String, s : String) : Void
    {
        var b = Bytes.ofString(s);
        var f = File.write(path, true);
        f.writeByte(0xEF);
        f.writeByte(0xBB);
        f.writeByte(0xBF);
        f.writeBytes(b, 0, b.length);
        f.close();
    }
    
    static function copyJson() : Void
    {
        var t:String = readUtf8File("..\\..\\CSharp\\Data\\WorldData.json");
        var j:Dynamic = Json.parse(t);
        var w:World = JsonTranslator.worldFromJson(j);

        var j2:Dynamic = JsonTranslator.worldToJson(w);
        var t2:String = Json.stringify(j2);
        writeUtf8File("..\\Data\\WorldData.json", t2);
    }

    static function main() : Void
    {
        copyJson();
    }
}
