import haxe.macro.Context;
import haxe.macro.Expr;
using Lambda;

class Functional
{
	@:macro	public static function fun(e : Array<Expr>)	: Expr
	{
		if (e.length <=	0) { Context.error("At least one argument is required.", Context.currentPos());	}
		var	body = e[e.length -	1];
		var	args = e.slice(0, e.length - 1);
		var	argExprs = new Array<FunctionArg>();
		
		for	(x in args)
		{
			var	xName :	String = null;
			switch (x.expr)
			{
				case EConst(c):
					switch (c)
					{
						case CIdent(s):
							xName =	s;
						default:
					}
				default:
			}
			if (xName == null) { Context.error("Identifier expected.", x.pos); }
			argExprs.push({name	: xName, opt : false, type : null, value : null});
		}
		
		var	Return : Expr =	{expr :	ExprDef.EReturn(body), pos : body.pos};
		return {expr : ExprDef.EFunction(null, {args : argExprs, ret : null, expr :	Return,	params : []}), pos : Context.currentPos()};
	}

	@:macro	public static function sub(e : Array<Expr>)	: Expr
	{
		if (e.length <=	0) { Context.error("At least one argument is required.", Context.currentPos());	}
		var	body = e[e.length -	1];
		var	args = e.slice(0, e.length - 1);
		var	argExprs = new Array<FunctionArg>();
		
		for	(x in args)
		{
			var	xName :	String = null;
			switch (x.expr)
			{
				case EConst(c):
					switch (c)
					{
						case CIdent(s):
							xName =	s;
						default:
					}
				default:
			}
			if (xName == null) { Context.error("Identifier expected.", x.pos); }
			argExprs.push({name	: xName, opt : false, type : null, value : null});
		}
		
		var	returnType = ComplexType.TPath({pack : [], name	: "Void", params : [], sub : null});
		return {expr : ExprDef.EFunction(null, {args : argExprs, ret : returnType, expr	: body,	params : []}), pos : Context.currentPos()};
	}
}
