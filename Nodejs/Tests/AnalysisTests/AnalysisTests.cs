﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.NodejsTools.Analysis;
using Microsoft.NodejsTools.Parsing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace AnalysisTests {
    [TestClass]
    public class AnalysisTests {
        [TestMethod]
        public void TestPrimitiveMembers() {
            string code = @"
var b = true;
var n = 42.0;
var s = 'abc';
function f() {
}
";
            var analysis = ProcessText(code);
            AssertUtil.ContainsAtLeast(
                GetMemberNames(analysis, "b"), 
                "valueOf"
            );
            AssertUtil.ContainsAtLeast(
                GetMemberNames(analysis, "n"),
                "toFixed", "toExponential"
            );
            AssertUtil.ContainsAtLeast(
                GetMemberNames(analysis, "s"),
                "anchor", "big", "indexOf"
            );
            AssertUtil.ContainsAtLeast(
                GetMemberNames(analysis, "f"),
                "apply"
            );
            
            AssertUtil.ContainsExactly(
                analysis.GetTypeIdsByIndex("b.valueOf", 0),
                BuiltinTypeId.Function
            );
            AssertUtil.ContainsExactly(
                analysis.GetTypeIdsByIndex("n.toFixed", 0),
                BuiltinTypeId.Function
            );
            AssertUtil.ContainsExactly(
                analysis.GetTypeIdsByIndex("s.big", 0),
                BuiltinTypeId.Function
            );
            AssertUtil.ContainsExactly(
                analysis.GetTypeIdsByIndex("f.apply", 0),
                BuiltinTypeId.Function
            ); 
        }

        [TestMethod]
        public void TestArrayForEach() {
            string code = @"
var arr = [1,2,3];
function f(a) {
    // here
}
arr.forEach(f);
";
            var analysis = ProcessText(code);
            AssertUtil.ContainsExactly(
                analysis.GetTypeIdsByIndex("a", code.IndexOf("// here")),
                BuiltinTypeId.Number
            );
        }

        [TestMethod]
        public void TestThisFlows() {
            string code = @"
function f() {
    this.func();
}
f.prototype.func = function() {
    this.value = 42;
}

var x = new f().value;
";
            var analysis = ProcessText(code);
            AssertUtil.ContainsExactly(
                analysis.GetTypeIdsByIndex("x", 0),
                BuiltinTypeId.Number
            ); 
        }

        /// <summary>
        /// Tests the internal [[Contruct]] method and makes sure
        /// we return the value if the function returns an object.
        /// </summary>
        [TestMethod]
        public void TestConstruct() {
            var code = @"function f() {
     return {abc:42};
}
var x = new f();
";
            var analysis = ProcessText(code);
            AssertUtil.ContainsExactly(
                analysis.GetTypeIdsByIndex("x", 0),
                BuiltinTypeId.Object
            );
            AssertUtil.ContainsExactly(
                analysis.GetTypeIdsByIndex("x.abc", 0),
                BuiltinTypeId.Number
            );

            code = @"function f() {
     return 42;
     return {abc:42};
}
var x = new f();
";
            analysis = ProcessText(code);
            AssertUtil.ContainsExactly(
                analysis.GetTypeIdsByIndex("x", 0),
                BuiltinTypeId.Object
            );
            AssertUtil.ContainsExactly(
                analysis.GetTypeIdsByIndex("x.abc", 0),
                BuiltinTypeId.Number
            );

            code = @"function f() {
     return 42;
}
var x = new f();
";
            analysis = ProcessText(code);
            AssertUtil.ContainsExactly(
                analysis.GetTypeIdsByIndex("x", 0),
                BuiltinTypeId.Object
            );
        }

        [TestMethod]
        public void TestConstructReturnFunction() {
            var code = @"function f() {
     function inner() {
     }
     inner.abc = 42;
     return inner;
}
var x = new f();
";
            var analysis = ProcessText(code);
            AssertUtil.ContainsAtLeast(
                analysis.GetTypeIdsByIndex("x", 0),
                BuiltinTypeId.Function
            );
            AssertUtil.ContainsExactly(
                analysis.GetTypeIdsByIndex("x.abc", 0),
                BuiltinTypeId.Number
            );
        }

        private static IEnumerable<string> GetMemberNames(ModuleAnalysis analysis, string name) {
            return analysis.GetMembersByIndex(name, 0).Select(x => x.Name);
        }



        [TestMethod]
        public void TestExports() {
            string code = @"
exports.num = 42;
module.exports.str = 'abc'
";
            var analysis = ProcessText(code);
            AssertUtil.ContainsExactly(
                analysis.GetTypeIdsByIndex("exports.num", 0),
                BuiltinTypeId.Number
            );
            AssertUtil.ContainsExactly(
                analysis.GetTypeIdsByIndex("exports.str", 0),
                BuiltinTypeId.String
            );
        }

        [TestMethod]
        public void TestAccessorDescriptor() {
            string code = @"
function f() {
}
Object.defineProperty(f, 'foo', {get: function() { return 42; }})
x = f.foo;
";
            var analysis = ProcessText(code);
            AssertUtil.ContainsExactly(
                analysis.GetTypeIdsByIndex("x", 0),
                BuiltinTypeId.Number
            );
        }

        [TestMethod]
        public void TestDefineProperties() {
            string code = @"
function f() {
}
Object.defineProperties(f, {abc:{get: function() { return 42; } } })
x = f.abc;
";
            var analysis = ProcessText(code);
            AssertUtil.ContainsExactly(
                analysis.GetTypeIdsByIndex("x", 0),
                BuiltinTypeId.Number
            );
        }

        [TestMethod]
        public void TestFunctionExpression() {
            string code = @"
var abc = {abc: function abcdefg() { } };
var x = abcdefg;
";
            var analysis = ProcessText(code);
            Assert.AreEqual(0, analysis.GetTypeIdsByIndex("x", 0).Count());
        }

        [TestMethod]
        public void TestGlobal() {
            var analysis = ProcessText(";");

            AssertUtil.ContainsExactly(
                analysis.GetTypeIdsByIndex("Number", 0),
                BuiltinTypeId.Function
            );
            /*
            analysis = ProcessText("var x = 42;");
            AssertUtil.ContainsExactly(
                analysis.GetTypeIdsByIndex("x", 0),
                BuiltinTypeId.Number
            );*/
        }

        [TestMethod]
        public void TestNumberFunction() {
            var analysis = ProcessText(";");

            foreach (var numberValue in new[] { "length", "MAX_VALUE", "MIN_VALUE", "NaN", "NEGATIVE_INFINITY", "POSITIVE_INFINITY" }) {
                AssertUtil.ContainsExactly(
                    analysis.GetTypeIdsByIndex("Number." + numberValue, 0),
                    BuiltinTypeId.Number
                );
            }

            foreach (var nullValue in new[] { "arguments", "caller" }) {
                AssertUtil.ContainsExactly(
                    analysis.GetTypeIdsByIndex("Number." + nullValue, 0),
                    BuiltinTypeId.Null
                );
            }

            AssertUtil.ContainsExactly(
                analysis.GetTypeIdsByIndex("Number.prototype", 0),
                BuiltinTypeId.Object
            );

            AssertUtil.ContainsExactly(
                analysis.GetTypeIdsByIndex("Number.name", 0),
                BuiltinTypeId.String
            );

            AssertUtil.ContainsExactly(
                analysis.GetTypeIdsByIndex("Number.isNaN(42)", 0),
                BuiltinTypeId.Boolean
            );

            AssertUtil.ContainsExactly(
                analysis.GetTypeIdsByIndex("Number.isFinite(42)", 0),
                BuiltinTypeId.Boolean
            );
        }

        [TestMethod]
        public void TestSimpleClosure() {
            var code = @" function f()
{
 var x = 42;
 function g() { return x }
 return g;
}";
            var analysis = ProcessText(code);
            AssertUtil.ContainsExactly(
                analysis.GetTypeIdsByIndex("f()()", 0),
                BuiltinTypeId.Number
            );
        }


        //[TestMethod]
        //public void ReproTestCase() {
        //    var analysis = ProcessText(File.ReadAllText(@"C:\Source\ExpressApp29\ExpressApp29\node_modules\jade\node_modules\with\node_modules\uglify-js\lib\scope.js"));
        //    Console.WriteLine("Done processing");
        //}

        [TestMethod]
        public void TestNumber() {
            var analysis = ProcessText("x = 42;");
            AssertUtil.ContainsExactly(
                analysis.GetTypeIdsByIndex("x", 0),
                BuiltinTypeId.Number
            );
            /*
            analysis = ProcessText("var x = 42;");
            AssertUtil.ContainsExactly(
                analysis.GetTypeIdsByIndex("x", 0),
                BuiltinTypeId.Number
            );*/
        }

        [TestMethod]
        public void TestVariableLookup() {
            var analysis = ProcessText("x = 42; y = x;");
            AssertUtil.ContainsExactly(
                analysis.GetTypeIdsByIndex("y", 0),
                BuiltinTypeId.Number
            );
        }

        [TestMethod]
        public void TestVariableDeclaration() {
            var analysis = ProcessText("var x = 42;");
            AssertUtil.ContainsExactly(
                analysis.GetTypeIdsByIndex("x", 0),
                BuiltinTypeId.Number
            );
        }

        [TestMethod]
        public void TestStringLiteral() {
            var analysis = ProcessText("x = 'abc';");
            AssertUtil.ContainsExactly(
                analysis.GetTypeIdsByIndex("x", 0),
                BuiltinTypeId.String
            );
        }

        [TestMethod]
        public void TestArrayLiteral() {
            var analysis = ProcessText("x = [1,2,3];");
            AssertUtil.ContainsExactly(
                analysis.GetTypeIdsByIndex("x[0]", 0),
                BuiltinTypeId.Number
            );
        }

        [TestMethod]
        public void TestTrueFalse() {
            var analysis = ProcessText("x = true;");
            AssertUtil.ContainsExactly(
                analysis.GetTypeIdsByIndex("x", 0),
                BuiltinTypeId.Boolean
            );

            analysis = ProcessText("x = false;");
            AssertUtil.ContainsExactly(
                analysis.GetTypeIdsByIndex("x", 0),
                BuiltinTypeId.Boolean
            );
        }

        [TestMethod]
        public void TestObjectLiteral() {
            var analysis = ProcessText("x = {abc:42};");
            AssertUtil.ContainsExactly(
                analysis.GetTypeIdsByIndex("x.abc", 0),
                BuiltinTypeId.Number
            );

            analysis = ProcessText("x = {42:42};");
            AssertUtil.ContainsExactly(
                analysis.GetTypeIdsByIndex("x[42]", 0),
                BuiltinTypeId.Number
            );

            analysis = ProcessText("x = {abc:42};");
            AssertUtil.ContainsExactly(
                analysis.GetTypeIdsByIndex("x['abc']", 0),
                BuiltinTypeId.Number
            );

            analysis = ProcessText("x = {}; x['abc'] = 42;");
            AssertUtil.ContainsExactly(
                analysis.GetTypeIdsByIndex("x.abc", 0),
                BuiltinTypeId.Number
            );

            analysis = ProcessText("x = {abc:42};");
            AssertUtil.ContainsExactly(
                analysis.GetTypeIdsByIndex("x[0, 'abc']", 0),
                BuiltinTypeId.Number
            );
        }

        [TestMethod]
        public void TestForLoop() {
            var analysis = ProcessText("for(var x in [1,2,3]) { }");
            AssertUtil.ContainsExactly(
                analysis.GetTypeIdsByIndex("x", 0),
                BuiltinTypeId.String
            );
        }

        [TestMethod]
        public void TestFunctionCreation() {
            var analysis = ProcessText(@"function f() {
}
f.prototype.abc = 42

var x = new f().abc;
");
            AssertUtil.ContainsExactly(
                analysis.GetTypeIdsByIndex("x", 0),
                BuiltinTypeId.Number
            );

            analysis = ProcessText(@"function f() {
}
f.abc = 42

var x = f.prototype.constructor.abc;
");
            AssertUtil.ContainsExactly(
                analysis.GetTypeIdsByIndex("x", 0),
                BuiltinTypeId.Number
            );

        }

        [TestMethod]
        public void TestPrototypeNoMerge() {
            var analysis = ProcessText(@"
var abc = Function.prototype;
abc.bar = 42;
function f() {
}
var x = new f().bar;
");
            AssertUtil.ContainsExactly(
                analysis.GetTypeIdsByIndex("x", 0)
            );
        }

        [TestMethod]
        public void TestMergeSpecialization() {
            var analysis = ProcessText(@"function merge(a, b){
  if (a && b) {
    for (var key in b) {
      a[key] = b[key];
    }
  }
  return a;
};


function f() {
}

f.abc = 42

function g() {
}

merge(g, f)
var x = g.abc;
");
            AssertUtil.ContainsExactly(
                analysis.GetTypeIdsByIndex("x", 0),
                BuiltinTypeId.Number
            );
        }

        [TestMethod]
        public void TestFunctionParametersMultipleCallers() {
            var analysis = ProcessText(@"function f(a) {
    return a;
}

var x = f(42);
var y = f('abc');
");
            AssertUtil.ContainsExactly(
                analysis.GetTypeIdsByIndex("x", 0),
                BuiltinTypeId.Number
            );
            AssertUtil.ContainsExactly(
                analysis.GetTypeIdsByIndex("y", 0),
                BuiltinTypeId.String
            );
        }

        [TestMethod]
        public void TestFunctionParameters() {
            var analysis = ProcessText(@"function x(a) { return a; }
var y = x(42);");
            AssertUtil.ContainsExactly(
                analysis.GetTypeIdsByIndex("y", 0),
                BuiltinTypeId.Number
            );
        }

        [TestMethod]
        public void TestFunction() {
            var analysis = ProcessText("function x() { }");
            AssertUtil.ContainsExactly(
                analysis.GetTypeIdsByIndex("x", 0),
                BuiltinTypeId.Function
            );

            analysis = ProcessText("function x() { return 42; }");
            AssertUtil.ContainsExactly(
                analysis.GetTypeIdsByIndex("x()", 0),
                BuiltinTypeId.Number
            );

            analysis = ProcessText("function x() { return 'abc'; }");
            AssertUtil.ContainsExactly(
                analysis.GetTypeIdsByIndex("x()", 0),
                BuiltinTypeId.String
            );

            analysis = ProcessText("function x() { return null; }");
            AssertUtil.ContainsExactly(
                analysis.GetTypeIdsByIndex("x()", 0),
                BuiltinTypeId.Null
            );

            analysis = ProcessText("function x() { return undefined; }");
            AssertUtil.ContainsExactly(
                analysis.GetTypeIdsByIndex("x()", 0),
                BuiltinTypeId.Undefined
            );

            analysis = ProcessText("function x() { return true; }");
            AssertUtil.ContainsExactly(
                analysis.GetTypeIdsByIndex("x()", 0),
                BuiltinTypeId.Boolean
            );

            analysis = ProcessText("function x() { return function() { }; }");
            AssertUtil.ContainsExactly(
                analysis.GetTypeIdsByIndex("x()", 0),
                BuiltinTypeId.Function
            );

            analysis = ProcessText("function x() { return { }; }");
            AssertUtil.ContainsExactly(
                analysis.GetTypeIdsByIndex("x()", 0),
                BuiltinTypeId.Object
            );
        }

        [TestMethod]
        public void TestNewFunction() {
            var analysis = ProcessText("function y() { return 42; }\r\nx = new y();");
            AssertUtil.ContainsExactly(
                analysis.GetTypeIdsByIndex("x", 0),
                BuiltinTypeId.Object
            );

            analysis = ProcessText("function f() { this.abc = 42; }\r\nx = new f();");
            AssertUtil.ContainsExactly(
                analysis.GetTypeIdsByIndex("x.abc", 0),
                BuiltinTypeId.Number
            );
        }

        [TestMethod]
        public void TestNewFunctionInstanceVariable() {
            var analysis = ProcessText("function f() { this.abc = 42; }\r\nx = new f();");
            AssertUtil.ContainsExactly(
                analysis.GetTypeIdsByIndex("x.abc", 0),
                BuiltinTypeId.Number
            );

            analysis = ProcessText(@"
function x() { this.abc = 42; }
function y() { this.abc = 'abc'; }
abcx = new x();
abcy = new y();");
            AssertUtil.ContainsExactly(
                analysis.GetTypeIdsByIndex("abcx.abc", 0),
                BuiltinTypeId.Number
            );
            AssertUtil.ContainsExactly(
                analysis.GetTypeIdsByIndex("abcy.abc", 0),
                BuiltinTypeId.String
            );
        }

        [TestMethod]
        public void TestSimplePrototype() {
            var analysis = ProcessText(@"
function f() {
}
f.abc = 42;

function j() {
}
j.prototype = f;

x = new j();
");
            AssertUtil.ContainsExactly(
                analysis.GetTypeIdsByIndex("x.abc", 0),
                BuiltinTypeId.Number
            );
        }

        [TestMethod]
        public void TestTypeOf() {
            var analysis = ProcessText("x = typeof 42;");
            AssertUtil.ContainsExactly(
                analysis.GetTypeIdsByIndex("x", 0),
                BuiltinTypeId.String
            );
        }

        [TestMethod]
        public void TestVariableShadowing() {
            var code = @"var a = 'abc';
function f() {
console.log(a);
var a = 100;
}
f();";
            var analysis = ProcessText(code);
            AssertUtil.ContainsExactly(
                analysis.GetTypeIdsByIndex("a", code.IndexOf("console.log")),
                BuiltinTypeId.Number
            );

            AssertUtil.ContainsExactly(
                analysis.GetTypeIdsByIndex("a", code.IndexOf("f();")),
                BuiltinTypeId.String
            );
        }

        [TestMethod]
        public void TestFunctionExpressionNameScoping() {
            var code = @"var x = function abc() {
    // here
    var x = 42;
    return abc.bar;
}
x.bar = 42;
";
            var analysis = ProcessText(code);
            
            AssertUtil.ContainsExactly(
                analysis.GetTypeIdsByIndex("abc", code.IndexOf("// here")),
                BuiltinTypeId.Function
            );

            AssertUtil.ContainsExactly(
                analysis.GetTypeIdsByIndex("x()", code.Length),
                BuiltinTypeId.Number
            );
        }


        [TestMethod]
        public void TestTypes() {
            var analysis = ProcessText("x = {undefined:undefined, number:42, string:'str', null:null, boolean:true, function: function() {}, object: {}}");
            var testCases = new[] { 
                new {Name="number", TypeId = BuiltinTypeId.Number},
                new {Name="string", TypeId = BuiltinTypeId.String},
                new {Name="null", TypeId = BuiltinTypeId.Null},
                new {Name="boolean", TypeId = BuiltinTypeId.Boolean},
                new {Name="object", TypeId = BuiltinTypeId.Object},
                new {Name="function", TypeId = BuiltinTypeId.Function},
                new {Name="undefined", TypeId = BuiltinTypeId.Undefined},
            };

            foreach (var testCase in testCases) {
                AssertUtil.ContainsExactly(
                    analysis.GetTypeIdsByIndex("x." + testCase.Name, 0),
                    testCase.TypeId
                );
                AssertUtil.ContainsExactly(
                    analysis.GetTypeIdsByIndex("typeof x." + testCase.Name, 0),
                    BuiltinTypeId.String
                );
                AssertUtil.ContainsExactly(
                    analysis.GetTypeIdsByIndex("x[typeof x." + testCase.Name + "]", 0),
                    testCase.TypeId
                );
            }
        }

        public static ModuleAnalysis ProcessText(string text) {
            var sourceUnit = GetSourceUnit(text);
            var state = new JsAnalyzer();
            var entry = state.AddModule("fob.js", null);
            Prepare(entry, sourceUnit);
            entry.Analyze(CancellationToken.None);

            return entry.Analysis;
        }

        public static void Prepare(IJsProjectEntry entry, TextReader sourceUnit) {
            var parser = new JSParser(sourceUnit.ReadToEnd());
            var ast = parser.Parse(new CodeSettings());
            entry.UpdateTree(ast, null);
        }

        public static TextReader GetSourceUnit(string text) {
            return new StringReader(text);
        }
    }

    static class AnalysisTestExtensions {
        public static IEnumerable<BuiltinTypeId> GetTypeIdsByIndex(this ModuleAnalysis analysis, string exprText, int index) {
            return analysis.GetValuesByIndex(exprText, index).Select(m => {
                return m.TypeId;
            });
        }
    }
}