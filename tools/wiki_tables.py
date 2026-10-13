"""Read literal Wiki Lua tables without executing any remote Lua code."""
from luaparser import ast
from luaparser.astnodes import *
import operator

def literal(node):
    if isinstance(node, String):
        return node.s.decode('utf-8') if isinstance(node.s, bytes) else node.s
    if isinstance(node, Number): return node.n
    if isinstance(node, TrueExpr): return True
    if isinstance(node, FalseExpr): return False
    if isinstance(node, Nil): return None
    if isinstance(node, UMinusOp): return -literal(node.operand)
    operators={AddOp:operator.add,SubOp:operator.sub,MultOp:operator.mul,FloatDivOp:operator.truediv}
    if type(node) in operators:
        a,b=literal(node.left),literal(node.right)
        if isinstance(a,(int,float)) and isinstance(b,(int,float)):
            return operators[type(node)](a,b)
    if isinstance(node, Table):
        result={}; implicit=1; duplicates=[]
        for field in node.fields:
            if field.key is None: key=implicit; implicit+=1
            elif isinstance(field.key,Name): key=field.key.id
            else: key=literal(field.key)
            value=literal(field.value)
            if key in result: duplicates.append({'key':key,'previous':result[key],'last':value})
            result[key]=value
        if duplicates: result['$duplicateFields']=duplicates
        if set(result)==set(range(1,len(result)+1)): return [result[i] for i in range(1,len(result)+1)]
        return result
    if isinstance(node, Name): return {'unresolved':node.id}
    return {'unresolved':ast.to_lua_source(node)}

def parse(text, variable=None):
    tree=ast.parse(text)
    for node in tree.body.body:
        if variable and isinstance(node,(LocalAssign,Assign)):
            for name,value in zip(node.targets,node.values):
                if isinstance(name,Name) and name.id==variable: return literal(value)
        elif variable is None and isinstance(node,Return):
            return literal(node.values[0])
    raise ValueError('Data table not found: '+str(variable))
