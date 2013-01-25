import haxe.macro.Context;
import haxe.macro.Expr;

class BuildDate
{
    @:macro public static function getBuildDate() : Expr
    {
        var date = Date.now().toString();
        return Context.makeExpr(date, Context.currentPos());
    }
}
