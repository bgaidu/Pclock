using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace TUnit
{
    /// <summary>
    /// 极简单元测试框架（零依赖，配合 csc 手动编译工作流）。
    /// 用法：测试方法加 [Test]，在 Main 中调用 TUnit.Runner.Run(new[]{typeof(XxxTests)});
    /// </summary>

    [AttributeUsage(AttributeTargets.Method)]
    public class TestAttribute : Attribute { }

    public static class Assert
    {
        public static void IsTrue(bool cond, string msg = null)
        {
            if (!cond) throw new Exception("IsTrue 失败" + (msg == null ? "" : "：" + msg));
        }
        public static void IsFalse(bool cond, string msg = null)
        {
            if (cond) throw new Exception("IsFalse 失败" + (msg == null ? "" : "：" + msg));
        }
        public static void AreEqual<T>(T expected, T actual, string msg = null)
        {
            if (!Equals(expected, actual))
                throw new Exception(string.Format("AreEqual 失败：期望 [{0}]，实际 [{1}]{2}",
                    expected, actual, msg == null ? "" : "：" + msg));
        }
        public static void GreaterOrEqual<T>(T actual, T expected, string msg = null) where T : IComparable<T>
        {
            if (actual.CompareTo(expected) < 0)
                throw new Exception(string.Format("GreaterOrEqual 失败：[{0}] 应 ≥ [{1}]{2}",
                    actual, expected, msg == null ? "" : "：" + msg));
        }
        public static void LessOrEqual<T>(T actual, T expected, string msg = null) where T : IComparable<T>
        {
            if (actual.CompareTo(expected) > 0)
                throw new Exception(string.Format("LessOrEqual 失败：[{0}] 应 ≤ [{1}]{2}",
                    actual, expected, msg == null ? "" : "：" + msg));
        }
        public static void Throws<TEx>(Action action, string msg = null) where TEx : Exception
        {
            try { action(); }
            catch (TEx) { return; }
            catch (Exception ex)
            {
                throw new Exception("Throws 失败：抛出 " + ex.GetType().Name + " 而非 " + typeof(TEx).Name);
            }
            throw new Exception("Throws 失败：未抛出任何异常" + (msg == null ? "" : "：" + msg));
        }
    }

    public static class Runner
    {
        public static int Run(Type[] testTypes)
        {
            int pass = 0, fail = 0;
            foreach (Type t in testTypes)
            {
                Console.WriteLine("== " + t.Name + " ==");
                object inst = Activator.CreateInstance(t);
                foreach (MethodInfo m in t.GetMethods(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (m.GetCustomAttribute<TestAttribute>() == null) continue;
                    if (m.GetParameters().Length != 0) continue;
                    try
                    {
                        m.Invoke(inst, null);
                        pass++;
                        Console.WriteLine("  PASS  " + m.Name);
                    }
                    catch (TargetInvocationException tie)
                    {
                        fail++;
                        Exception inner = tie.InnerException ?? tie;
                        Console.WriteLine("  FAIL  " + m.Name + " : " + inner.Message);
                    }
                    catch (Exception ex)
                    {
                        fail++;
                        Console.WriteLine("  FAIL  " + m.Name + " : " + ex.Message);
                    }
                }
            }
            Console.WriteLine("\n结果：" + pass + " 通过, " + fail + " 失败");
            return fail;
        }
    }
}
