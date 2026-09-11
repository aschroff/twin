using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace EditModeTests
{
    /// <summary>
    /// Keeps the process landscape honest: every test carries exactly one address.
    /// </summary>
    /// <remarks>
    /// A test without a category is invisible in the map, and the map then claims a coverage that
    /// is not there. A test with two addresses is worse - it is counted twice and nobody can say
    /// which process owns it. Neither shows up as a failure anywhere else, which is why it is
    /// checked here rather than left to review.
    /// </remarks>
    [Category(Processes.Technical)]
    public class TestCategoriesGuardTests
    {
        private static readonly string[] TestAssemblies = { "EditModeTests", "PlayModeTests" };

        private static HashSet<string> KnownCategories()
        {
            var known = new HashSet<string>();
            foreach (Type holder in new[] { typeof(Processes), typeof(Chains) })
            {
                foreach (FieldInfo field in holder.GetFields(BindingFlags.Public | BindingFlags.Static))
                {
                    if (field.IsLiteral && field.FieldType == typeof(string))
                    {
                        known.Add((string)field.GetRawConstantValue());
                    }
                }
            }
            return known;
        }

        private static IEnumerable<Assembly> LoadedTestAssemblies()
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => TestAssemblies.Contains(a.GetName().Name));
        }

        private static bool IsTest(MethodInfo method)
        {
            return method.GetCustomAttributes(typeof(TestAttribute), true).Length > 0
                   || method.GetCustomAttributes(typeof(UnityTestAttribute), true).Length > 0;
        }

        /// <summary>Both the method's own categories and the ones its class hands down.</summary>
        private static List<string> CategoriesOf(MethodInfo method)
        {
            var names = new List<string>();
            foreach (CategoryAttribute c in method.GetCustomAttributes(typeof(CategoryAttribute), true))
            {
                names.Add(c.Name);
            }

            for (Type type = method.DeclaringType; type != null; type = type.BaseType)
            {
                foreach (CategoryAttribute c in type.GetCustomAttributes(typeof(CategoryAttribute), false))
                {
                    names.Add(c.Name);
                }
            }

            return names;
        }

        [Test]
        public void BothTestAssembliesAreLoaded_soThisGuardSeesEverything()
        {
            CollectionAssert.AreEquivalent(
                TestAssemblies,
                LoadedTestAssemblies().Select(a => a.GetName().Name).ToArray(),
                "A test assembly is not loaded, so this guard would pass without having looked at it.");
        }

        [Test]
        public void EveryTestCarriesExactlyOneAddressInTheProcessLandscape()
        {
            HashSet<string> known = KnownCategories();
            var problems = new List<string>();

            foreach (Assembly assembly in LoadedTestAssemblies())
            {
                foreach (Type type in assembly.GetTypes())
                {
                    if (type.IsAbstract)
                    {
                        continue;
                    }

                    foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                    {
                        if (!IsTest(method))
                        {
                            continue;
                        }

                        // Tools that are driven through the test runner rather than run with the
                        // suite mark themselves [Explicit] - the template library generator does.
                        // They are not part of the landscape and are not asked for an address.
                        if (method.GetCustomAttributes(typeof(ExplicitAttribute), true).Length > 0
                            || method.DeclaringType.GetCustomAttributes(typeof(ExplicitAttribute), true).Length > 0)
                        {
                            continue;
                        }

                        List<string> categories = CategoriesOf(method).Where(known.Contains).ToList();
                        string where = type.FullName + "." + method.Name;

                        if (categories.Count == 0)
                        {
                            problems.Add(where + " — no category. Give it one from Processes or Chains.");
                        }
                        else if (categories.Count > 1)
                        {
                            problems.Add(where + " — " + categories.Count + " categories ("
                                         + string.Join(", ", categories) + "). A test belongs to one process.");
                        }
                    }
                }
            }

            Assert.IsEmpty(problems,
                "Tests that are not in the process landscape (see Assets/Tests/PROCESS_LANDSCAPE.md):\n"
                + string.Join("\n", problems));
        }
    }
}
