﻿using System;
using System.Diagnostics;
using NUnit.Framework;
using winsw;
using System.IO;
using winsw.Util;
using System.Collections.Generic;

namespace winswTests.Util
{

    [TestFixture]
    class ProcessHelperTest
    {
        /// <summary>
        /// Also reported as <a href="https://issues.jenkins-ci.org/browse/JENKINS-42744">JENKINS-42744</a>
        /// </summary>
        [Test]
        public void ShouldPropagateVariablesInUppercase()
        {
            var tmpDir = FilesystemTestHelper.CreateTmpDirectory();
            String envFile = Path.Combine(tmpDir, "env.properties");
            String scriptFile = Path.Combine(tmpDir, "printenv.bat");
            File.WriteAllText(scriptFile, "set > " + envFile);


            Process proc = new Process();
            var ps = proc.StartInfo;
            ps.FileName = scriptFile;

            ProcessHelper.StartProcessAndCallbackForExit(proc);
            var exited = proc.WaitForExit(5000);
            if (!exited)
            {
                Assert.Fail("Process " + proc + " didn't exit after 5 seconds");
            }

            // Check several veriables, which are expected to be in Uppercase
            var envVars = FilesystemTestHelper.parseSetOutput(envFile);
            String[] keys = new String[envVars.Count];
            envVars.Keys.CopyTo(keys, 0);
            String availableVars = "[" + String.Join(",", keys) + "]";
            Assert.That(envVars.ContainsKey("PROCESSOR_ARCHITECTURE"), "No PROCESSOR_ARCHITECTURE in the injected vars: " + availableVars);
            Assert.That(envVars.ContainsKey("COMPUTERNAME"), "No COMPUTERNAME in the injected vars: " + availableVars);
            Assert.That(envVars.ContainsKey("PATHEXT"), "No PATHEXT in the injected vars: " + availableVars);
            
            // And just ensure that the parsing logic is case-sensitive
            Assert.That(!envVars.ContainsKey("computername"), "Test error: the environment parsing logic is case-insensitive");

        }

        [Test]
        public void ShouldNotHangWhenWritingLargeStringToStdOut()
        {
            var tmpDir = FilesystemTestHelper.CreateTmpDirectory();
            String scriptFile = Path.Combine(tmpDir, "print_lots_to_stdout.bat");
            var lotsOfStdOut = string.Join("", _Range(1,1000));
            File.WriteAllText(scriptFile, string.Format("echo \"{0}\"", lotsOfStdOut));

            Process proc = new Process();
            var ps = proc.StartInfo;
            ps.FileName = scriptFile;

            ProcessHelper.StartProcessAndCallbackForExit(proc);
            var exited = proc.WaitForExit(5000);
            if (!exited)
            {
                Assert.Fail("Process " + proc + " didn't exit after 5 seconds");
            }
        }

        [Test]
        public void ShouldGetChildPids()
        {
            const int childCount = 4;
            string sleep5secAndExit = "timeout 5" + Environment.NewLine + "exit";
            var tmpDir = FilesystemTestHelper.CreateTmpDirectory();
            string childScriptFile = Path.Combine(tmpDir, "child.bat");
            string parentScriptFile = Path.Combine(tmpDir, "parent.bat");
            
            File.WriteAllText(childScriptFile, sleep5secAndExit);
            File.WriteAllLines(parentScriptFile, Repeat("start child.bat", childCount));
            File.AppendAllText(parentScriptFile, "pause");

            Process proc = new Process();
            var ps = proc.StartInfo;
            ps.FileName = parentScriptFile;
            ps.WorkingDirectory = tmpDir;
            proc.Start();

            proc.WaitForExit(1000);
            var childs = ProcessHelper.GetChildPids(proc.Id);
            Assert.AreEqual(childCount + 1, childs.Count); // +1 for conhost
            if (!proc.WaitForExit(5000))
            {
                proc.Kill();
            }
        }

        [Test]
        public void ShouldKillChild()
        {
            const int childCount = 4;
            string sleep20sec = "timeout 20";
            var tmpDir = FilesystemTestHelper.CreateTmpDirectory();
            string childScriptFile = Path.Combine(tmpDir, "child.bat");
            string parentScriptFile = Path.Combine(tmpDir, "parent.bat");

            File.WriteAllText(childScriptFile, sleep20sec);
            File.WriteAllLines(parentScriptFile, Repeat("start child.bat", childCount));
            File.AppendAllText(parentScriptFile, "pause");

            Process proc = new Process();
            var ps = proc.StartInfo;
            ps.FileName = parentScriptFile;
            ps.WorkingDirectory = tmpDir;
            proc.Start();
            proc.WaitForExit(1000);

            ProcessHelper.StopProcessAndChildren(proc.Id, TimeSpan.FromSeconds(1), false);

            Assert.AreEqual(0, ProcessHelper.GetChildPids(proc.Id).Count);
        }

        private static T[] Repeat<T>(T obj, int count)
        {
            var arr = new T[count];
            for (int i = 0; i < count; i++)
            {
                arr[i] = obj;
            }

            return arr;

        }

        private string[] _Range(int start, int limit)
        {
            var range = new List<string>();
            for(var i = start; i<limit; i++)
            {
                range.Add(i.ToString());
            }
            return range.ToArray();
        }
    }
}
