#region license

// Copyright 2004-2011 Castle Project - http://www.castleproject.org/
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
// http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

#endregion

namespace Castle.Services.Transaction.Tests
{
	using System;
	using System.Collections.Generic;
	using System.Threading;

	using Castle.Services.Transaction.IO;
	using Castle.Services.Transaction.Tests.Framework;
	using Facilities.Transactions.Tests.TestClasses;
	using NUnit.Framework;
	using Exts = Facilities.Transactions.Tests.TestClasses.Exts;

	[TestFixture, Ignore("TODO")]
	public class FileTransactions_Directory_Tests : TxFTestFixtureBase
	{
		#region Setup/Teardown

		private string _DllPath;
		private readonly List<string> _InfosCreated = new List<string>();
		private static volatile object _serializer = new object();

		[SetUp]
		public void CleanOutListEtc()
		{
			Monitor.Enter(_serializer);
			_InfosCreated.Clear();
		}

		[TearDown]
		public void RemoveAllCreatedFiles()
		{
			foreach (var filePath in _InfosCreated)
			{
				if (File.Exists(filePath))
					File.Delete(filePath);
				else if (Directory.Exists(filePath))
					Directory.Delete(filePath);
			}

			if (Directory.Exists("testing"))
				Directory.Delete("testing", true);

			Monitor.Exit(_serializer);
		}

		[TestFixtureSetUp]
		public void Setup()
		{
			_DllPath = Environment.CurrentDirectory;
			Exts.Combine(_DllPath, "..\\..\\Kernel");
		}

		#endregion

		[Test]
		public void NoCommit_MeansNoDirectory()
		{
			var directoryPath = "testing";
			Assert.That(Directory.Exists(directoryPath), Is.False);

			using (ITransaction tx = new FileTransaction())
			{
				Directory.Create(directoryPath);
				Assert.IsTrue(Directory.Exists(directoryPath));
				tx.Dispose();
			}

			Assert.That(!Directory.Exists(directoryPath));
		}

		[Test]
		public void NonExistentDir()
		{
			using (var t = new FileTransaction())
			{
				var dir = (t as IDirectoryAdapter);
				Assert.IsFalse(dir.Exists("/hahaha"));
				Assert.IsFalse(dir.Exists("another_non_existent"));
				dir.Create("existing");
				Assert.IsTrue(dir.Exists("existing"));
			}
			// no commit
			Assert.IsFalse(Directory.Exists("existing"));
		}

		[Test, Description("We are not in a distributed transaction if there is no transaction scope.")]
		public void NotUsingTransactionScope_IsNotDistributed_AboveNegated()
		{
			using (ITransaction tx = new FileTransaction("Not distributed transaction"))
			{
				Assert.That(System.Transactions.Transaction.Current, Is.Null);
				tx.Complete();
			}
		}

		[Test]
		public void ExistingDirWithTrailingBackslash()
		{
			// From http://msdn.microsoft.com/en-us/library/aa364419(VS.85).aspx
			// An attempt to open a search with a trailing backslash always fails.
			// --> So I need to make it succeed.
			using (var t = new FileTransaction())
			{
				var dir = t as IDirectoryAdapter;
				dir.Create("something");
				Assert.That(dir.Exists("something"));
				Assert.That(dir.Exists("something\\"));
			}
		}

		[Test]
		public void CreatingFolder_InTransaction_AndCommitting_MeansExistsAfter()
		{
			string directoryPath = "testing";
			Assert.That(Directory.Exists(directoryPath), Is.False);

			using (ITransaction tx = new FileTransaction())
			{
				Directory.Create(directoryPath);
				tx.Complete();
			}

			Assert.That(Directory.Exists(directoryPath));

			Directory.Delete(directoryPath);
		}

		[Test]
		public void CanCreate_AndFind_Directory_WithinTx()
		{
			using (ITransaction tx = new FileTransaction("s"))
			{
				var da = (IDirectoryAdapter) tx;
				Assert.That(da.Exists("something"), Is.False);
				da.Create("something");
				Assert.That(da.Exists("something"));
				tx.Rollback();
			}
		}

		[Test]
		public void CanCreateDirectory_NLengths_DownInNonExistentDirectory()
		{
			string directoryPath = "testing/apa/apa2";
			Assert.That(Directory.Exists(directoryPath), Is.False);

			using (ITransaction t = new FileTransaction())
			{
				Directory.Create(directoryPath);
				t.Complete();
			}

			Assert.That(Directory.Exists(directoryPath));
			Directory.Delete(directoryPath);
		}
		[Test]
		public void CanDelete_NonRecursively_EmptyDir()
		{
			// 1. create dir
			string dir = _DllPath.CombineAssert("testing");

			// 2. test it
			using (ITransaction t = new FileTransaction("Can delete empty directory"))
			{
				Assert.That(((IDirectoryAdapter)t).Delete(dir, false), "Successfully deleted.");
				t.Complete();
			}
		}

		[Test]
		public void CanDelete_Recursively()
		{
			// 1. Create directory
			string pr = Exts.Combine(_DllPath, "testing");
			Directory.CreateDirectory(pr);
			Directory.CreateDirectory(Exts.Combine(pr, "one"));
			Directory.CreateDirectory(Exts.Combine(pr, "two"));
			Directory.CreateDirectory(Exts.Combine(pr, "three"));

			// 2. Write contents
			File.WriteAllLines(Exts.Combine(pr, "one", "fileone"), new[] { "Hello world", "second line" });
			File.WriteAllLines(Exts.Combine(pr, "one", "filetwo"), new[] { "two", "second line" });
			File.WriteAllLines(Exts.Combine(pr, "two", "filethree"), new[] { "three", "second line" });

			// 3. test
			using (ITransaction t = new FileTransaction())
			{
				Assert.IsTrue(((IDirectoryAdapter) t).Delete(pr, true));
				t.Complete();
			}
		}

		[Test]
		public void CanNotDelete_NonRecursively_NonEmptyDir()
		{
			// 1. create dir and file
			string dir = _DllPath.CombineAssert("testing");
			string file = Exts.Combine(dir, "file");
			File.WriteAllText(file, "hello");

			// 2. test it
			using (ITransaction t = new FileTransaction("Can not delete non-empty directory"))
			{
				Assert.That(Directory.Delete(dir, false),
							Is.False,
							"Did not delete non-empty dir.");
				
				File.Delete(file);

				Assert.That(Directory.Delete(dir, false),
							"After deleting the file in the folder, the folder is also deleted.");

				t.Complete();
			}
		}
	}
}