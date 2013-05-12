﻿/* 
 * Class: GregValure.NaturalDocs.CLI.Application
 * ____________________________________________________________________________
 * 
 * The main application class for the command line interface to Natural Docs.
 */

// This file is part of Natural Docs, which is Copyright © 2003-2013 Greg Valure.
// Natural Docs is licensed under version 3 of the GNU Affero General Public License (AGPL)
// Refer to License.txt for the complete details


using System;
using System.Collections.Generic;
using GregValure.NaturalDocs.Engine;
using GregValure.NaturalDocs.Engine.Errors;


namespace GregValure.NaturalDocs.CLI
	{
	public static class Application
		{
		
		// Group: Types
		// __________________________________________________________________________
		
		
		/* enum: ParseCommandLineResult
		 * 
		 * The result returned from <ParseCommandLine()>.
		 * 
		 * OK - The command line was OK and execution should continue.
		 * Error - There was an error on the command line.
		 * InformationalExit - The program was asked for information, such as the version number, that 
		 *								<ParseCommandLine()> provided and now execution should cease.
		 */
		public enum ParseCommandLineResult : byte
			{  OK, Error, InformationalExit  };
			
			
			
		// Group: Constants
		// __________________________________________________________________________
		
		
		/* Constant: StatusInterval
		 * The amount of time in milliseconds that must go by before a status update.
		 */
		public const int StatusInterval = 5000;
			
		/* Constant: DelayedMessageThreshold
		 * The amount of time in milliseconds that certain operations must take before they warrant a status update.
		 */
		public const int DelayedMessageThreshold = 1500;
			
						

		// Group: Functions
		// __________________________________________________________________________


		/* Function: Main
		 * The program entry point.
		 */
		public static void Main (string[] commandLine)
			{
			#if SHOW_EXECUTION_TIME
				long startProgram = System.DateTime.Now.Ticks;
				long endProgram = -1;
				long startParsing = -1;
				long endParsing = -1;
				long startResolving = -1;
				long endResolving = -1;
				long startBuilding = -1;
				long endBuilding = -1;
			#endif

			#if PAUSE_BEFORE_EXIT
				bool pauseBeforeExit = true;
			#elif PAUSE_ON_ERROR
				bool pauseBeforeExit = false;
			#endif
			
			bool gracefulExit = false;

			try
				{
				ErrorList startupErrors = new ErrorList();

				NaturalDocs.Engine.Instance.Create();
				
				ParseCommandLineResult parseCommandLineResult = ParseCommandLine(commandLine, startupErrors);

				if (parseCommandLineResult == ParseCommandLineResult.OK)
					{
					bool rebuildAllOutputFromCommandLine = Engine.Instance.Config.RebuildAllOutput;
					bool reparseEverythingFromCommandLine = Engine.Instance.Config.ReparseEverything;


					// Heading

					string version = Engine.Instance.VersionString;
					int parenthesisIndex = version.IndexOf('(');

					if (parenthesisIndex == -1)
						{
						System.Console.WriteLine();
						System.Console.Write(
							Engine.Locale.Get("NaturalDocs.CLI", "Status.Start(version).multiline", version)
							);
						}
					else
						{
						string subversion = version.Substring(parenthesisIndex);
						version = version.Substring(0, parenthesisIndex).TrimEnd();

						System.Console.WriteLine();
						System.Console.Write(
							Engine.Locale.Get("NaturalDocs.CLI", "Status.Start(version,subversion).multiline", version, subversion)
							);
						}

					NaturalDocs.Engine.Instance.AddStartupWatcher(new EngineStartupWatcher());

					if (NaturalDocs.Engine.Instance.Start(startupErrors) == true)
						{


						// File Search
						
						using ( StatusManagers.FileSearch statusManager = new StatusManagers.FileSearch() )
							{
							statusManager.Start();
							
							Multithread("File Adder", Engine.Instance.Files.WorkOnAddingAllFiles);
							
							statusManager.End();
							}
							
						Engine.Instance.Files.DeleteFilesNotInFileSources( Engine.Delegates.NeverCancel );
							
						
						// Rebuild notice

						string alternateStartMessage = null;
						
						if (reparseEverythingFromCommandLine || rebuildAllOutputFromCommandLine)
							{  alternateStartMessage = "Status.RebuildEverythingByRequest";  }
						else if (Engine.Instance.Config.ReparseEverything && Engine.Instance.Config.RebuildAllOutput)
							{  alternateStartMessage = "Status.RebuildEverythingAutomatically";  }
							
							
						// Parsing
						
						#if SHOW_EXECUTION_TIME
							startParsing = System.DateTime.Now.Ticks;
						#endif

						using ( StatusManagers.Parsing statusManager = new StatusManagers.Parsing(alternateStartMessage) )
							{
							statusManager.Start();

							Multithread("Parser", Engine.Instance.Files.WorkOnProcessingChanges);							
							
							statusManager.End();
							}
							
						#if SHOW_EXECUTION_TIME
							endParsing = System.DateTime.Now.Ticks;
						#endif

							
						// Resolving
						
						#if SHOW_EXECUTION_TIME
							startResolving = System.DateTime.Now.Ticks;
						#endif

						using ( StatusManagers.ResolvingLinks statusManager = new StatusManagers.ResolvingLinks() )
							{
							statusManager.Start();

							Multithread("Resolver", Engine.Instance.CodeDB.WorkOnResolvingLinks);
							
							statusManager.End();
							}
							
						#if SHOW_EXECUTION_TIME
							endResolving = System.DateTime.Now.Ticks;
						#endif

							
						// Building
						
						#if SHOW_EXECUTION_TIME
							startBuilding = System.DateTime.Now.Ticks;
						#endif

						using ( StatusManagers.Building statusManager = new StatusManagers.Building() )
							{
							statusManager.Start();

							Multithread("Builder", Engine.Instance.Output.WorkOnUpdatingOutput);
							Multithread("Finalizer", Engine.Instance.Output.WorkOnFinalizingOutput);							
							
							statusManager.End();
							}
							
						#if SHOW_EXECUTION_TIME
							endBuilding = System.DateTime.Now.Ticks;
						#endif

							
						// End
						
						Engine.Instance.Cleanup(Delegates.NeverCancel);
						
						System.Console.Write(
							Engine.Locale.Get("NaturalDocs.CLI", "Status.End.multiline")
							);
						System.Console.WriteLine();
						
						gracefulExit = true;

						}
					else
						{  
						HandleErrorList(startupErrors); 
						
						#if PAUSE_ON_ERROR
							pauseBeforeExit = true;
						#endif
						}
					}
				
				else if (parseCommandLineResult == ParseCommandLineResult.InformationalExit)
					{
					gracefulExit = true;
					}

				else
					{
					HandleErrorList(startupErrors);
					
					#if PAUSE_ON_ERROR
						pauseBeforeExit = true;
					#endif
					}
				}

			catch (Exception e)
				{  
				HandleException(e);  
				
				#if PAUSE_ON_ERROR
					pauseBeforeExit = true;
				#endif
				}
				
			finally
				{
				Engine.Instance.Dispose(gracefulExit);
				}
				
			#if SHOW_EXECUTION_TIME

				endProgram = DateTime.Now.Ticks;

				System.Console.WriteLine();

				if (startParsing != -1 && endParsing != -1)
					{  System.Console.WriteLine("Parsing time: {0:#,0}k ticks", (endParsing - startParsing) / 1000);  }
				if (startResolving != -1 && endResolving != -1)
					{  System.Console.WriteLine("Resolving time: {0:#,0}k ticks", (endResolving - startResolving) / 1000);  }
				if (startBuilding != -1 && endBuilding != -1)
					{  System.Console.WriteLine("Building time: {0:#,0}k ticks", (endBuilding - startBuilding) / 1000);  }
				if (startProgram != -1 && endProgram != -1)
					{  System.Console.WriteLine("Total time: {0:#,0}k ticks", (endProgram - startProgram) / 1000);  }

			#endif

			#if PAUSE_BEFORE_EXIT || PAUSE_ON_ERROR
				if (pauseBeforeExit)
					{
					System.Console.WriteLine();
					System.Console.WriteLine("Press any key to continue...");
					System.Console.ReadKey(true);
					}
			#endif
			}
			
			
		/* Function: Multithread
		 * 
		 * Executes the task across multiple threads.  The function passed must be suitably thread safe.  This
		 * function will not return until the task is complete.
		 * 
		 * Parameters:
		 * 
		 *		threadName - What the execution threads should be named.  "Thread #" will be appended so "Builder" will lead to 
		 *								 names like "Builder Thread 3".  This is important to specify because thread names are reported in 
		 *								 exceptions and crash reports.
		 *		task - The task to execute.  This must be thread safe.
		 */
		static public void Multithread (string threadName, CancellableTask task)
			{
			Engine.Thread[] threads = new Engine.Thread[ Engine.Instance.Config.BackgroundThreadsPerTask ];

			for (int i = 0; i < threads.Length; i++)
				{  
				Engine.Thread thread = new Engine.Thread();

				thread.Name = threadName + " Thread " + (i + 1);
				thread.Task = task;
				thread.CancelDelegate = Engine.Delegates.NeverCancel;
				thread.Priority = System.Threading.ThreadPriority.BelowNormal;

				threads[i] = thread;
				}

			foreach (var thread in threads)
				{  thread.Start();  }

			foreach (var thread in threads)
				{  thread.Join();  }

			foreach (var thread in threads)
				{  thread.ThrowExceptions();  }
			}


		/* Function: ParseCommandLine
		 * 
		 * Parses the command line and applies the relevant settings in in <NaturalDocs.Engine's> modules.  If there were 
		 * errors they will be placed on errorList and it will return <ParseCommandLineResult.Error>.  If the command line 
		 * was used to ask for information such as the program version, it will write it to the console and return
		 * <ParseCommandLineResult.InformationalExit>.
		 * 
		 * Supported:
		 * 
		 *		- -i, --input, --source
		 *		- -o, --output
		 *		- -p, --project, --projectconfig --projectconfiguration
		 *		- -w, --data, --workingdata
		 *		- -xi, --exclude-input, --exclude-source
		 *		- -xip, --exclude-input-pattern, --exclude-source-pattern
		 *		- -img, --images
		 *		- -t, --tab, --tabwidth, --tablength
		 *		- -nag, --noautogroup
		 *		- -s, --style, --defaultstyle
		 *		- -r, --rebuild
		 *		- -ro, --rebuildoutput
		 *		- -h, --help
		 *		- -?
		 *		- -v, --version
		 *		
		 * Unsupported so far:
		 * 
		 *		- -do, --documentedonly
		 *		- -cs, --charset, --characterset
		 *		- -q, --quiet
		 *		
		 * No longer supported:
		 * 
		 *		- -ho, --headersonly
		 *		- -s Custom, --style Custom
		 *		- -ag, --autogroup
		 */
		public static ParseCommandLineResult ParseCommandLine (string[] commandLine, ErrorList errorList)
			{
			int index = 0;
			int originalErrorCount = errorList.Count;
			
			Engine.Collections.StringToStringTable synonyms = new Engine.Collections.StringToStringTable( 
				false, false, // Case-insensitivity is handled elsewhere and we don't need normalization
				"input", "-i", "source", "-i", 
				"output", "-o",
				"excludeinput", "-xi", "excludesource", "-xi",
				"excludeinputpattern", "-xip", "excludesourcepattern", "-xip",
				"project", "-p", "projectconfig", "-p", "projectconfiguration", "-p",
				"data", "-w", "workingdata", "-w",
				"images", "-img", "image", "-img",
				"tab", "-t", "tabwidth", "-t", "tablength", "-t",
				"noautogroup", "-nag",
				"style", "-s", "defaultstyle", "-s",
				"help", "-h",
				"version", "-v",
				"rebuild", "-r",
				"rebuildoutput", "-ro"
				);
				
			while (index < commandLine.Length)
				{
				string command;
				
				// If the first parameter doesn't start with -, assume it's the project folder or file specified without -p.
				if (index == 0 && commandLine[0][0] != '-')
					{
					command = "-p";
					}
				else
					{
					command = commandLine[index].ToLower();
					index++;
					
					// Convert the --something form to the short form.
					if (command.Length > 2 && command[1] == '-')
						{
						// This allows things like --project-config to work just as well as --projectconfig.
						command = command.Replace("-", "");
						
						if (synonyms.ContainsKey(command))
							{  command = synonyms[command];  }
						// Otherwise let it go through.  The error handling for the short form with take care of it.
						}
					}					
					
					
				// Input folders
					
				if (command == "-i")
					{
					Path folder = Engine.Path.FromCommandLine(commandLine, ref index);

					if (folder.IsRelative)
						{  folder = System.Environment.CurrentDirectory + "/" + folder;  }
					
					Engine.Config.Entries.InputFolder entry = 
						new Engine.Config.Entries.InputFolder(folder, Engine.Files.InputType.Source);

					Engine.Instance.Config.CommandLineConfig.Entries.Add(entry);
					}
					

					
				// Ignored input folders
					
				else if (command == "-xi")
					{
					Path folder = Engine.Path.FromCommandLine(commandLine, ref index);
					
					if (folder.IsRelative)
						{  folder = System.Environment.CurrentDirectory + "/" + folder;  }
					
					Engine.Config.Entries.IgnoredSourceFolder entry = new Engine.Config.Entries.IgnoredSourceFolder(folder);

					Engine.Instance.Config.CommandLineConfig.Entries.Add(entry);
					}
					
					
					
				// Ignored input folder patterns
					
				else if (command == "-xip")
					{
					System.Text.StringBuilder pattern = new System.Text.StringBuilder();
					
					while (index < commandLine.Length && commandLine[index][0] != '-')
						{
						if (pattern.Length != 0)
							{  pattern.Append(' ');  }
							
						pattern.Append(commandLine[index]);
						index++;
						}

					Engine.Config.Entries.IgnoredSourceFolderPattern entry = 
						new Engine.Config.Entries.IgnoredSourceFolderPattern(pattern.ToString());

					Engine.Instance.Config.CommandLineConfig.Entries.Add(entry);
					}
					
					
					
				// Image folders
					
				else if (command == "-img")
					{
					Path folder = Engine.Path.FromCommandLine(commandLine, ref index);
					
					if (folder.IsRelative)
						{  folder = System.Environment.CurrentDirectory + "/" + folder;  }
					
					Engine.Config.Entries.InputFolder entry = 
						new Engine.Config.Entries.InputFolder(folder, Engine.Files.InputType.Image);
					}
					
					
					
				// Output folders
				
				else if (command == "-o")
					{
					string format = commandLine[index].ToLower();
					index++;
					
					if (index < commandLine.Length && commandLine[index][0] != '-')
						{
						Path folder = Engine.Path.FromCommandLine(commandLine, ref index);
						
						if (folder.IsRelative)
							{  folder = System.Environment.CurrentDirectory + "/" + folder;  }
					
						Engine.Config.OutputEntry entry = null;

						if (format == "html" || format == "framedhtml")
							{  entry = new Engine.Config.Entries.HTMLOutputFolder(folder);  }
						else if (format == "xml")
							{  entry = new Engine.Config.Entries.XMLOutputFolder(folder);  }
						else
							{
							errorList.Add( 
								Locale.Get("NaturalDocs.CLI", "CommandLine.UnrecognizedOutputFormat(format)", format)
								);
							}

						if (entry != null)
							{  Engine.Instance.Config.CommandLineConfig.Entries.Add(entry);  }
						}
						
					else
						{
						errorList.Add(
							Locale.Get("NaturalDocs.CLI", "CommandLine.OutputMustIncludeFormat")
							);
						}
					}
					
					
					
				// Project configuration folder
					
				else if (command == "-p")
					{
					Path folder = Engine.Path.FromCommandLine(commandLine, ref index);
						
					if (folder.IsRelative)
						{  folder = System.Environment.CurrentDirectory + "/" + folder;  }

					// Accept the parameter being to Project.txt instead of the folder.
					if (folder.NameWithoutPath.ToLower() == "project.txt")
						{  folder = folder.ParentFolder;  }
						
					if ( !String.IsNullOrEmpty(Engine.Instance.Config.ProjectConfigFolder) )
						{
						errorList.Add( 
							Locale.Get("NaturalDocs.CLI", "CommandLine.OnlyOneProjectConfigFolder")
							);
						}
					else
						{  Engine.Instance.Config.ProjectConfigFolder = folder;  }
					}
					
					

				// Working data folder
					
				else if (command == "-w")
					{
					Path folder = Engine.Path.FromCommandLine(commandLine, ref index);
						
					if (folder.IsRelative)
						{  folder = System.Environment.CurrentDirectory + "/" + folder;  }

					if ( !String.IsNullOrEmpty(Engine.Instance.Config.WorkingDataFolder) )
						{
						errorList.Add( 
							Locale.Get("NaturalDocs.CLI", "CommandLine.OnlyOneWorkingDataFolder")
							);
						}
					else
						{  Engine.Instance.Config.WorkingDataFolder = folder;  }
					}
					
					
				
				// Tab Width
				// Supports -t 4 and -t4.  Doesn't support --tablength4.
				
				else if (command.StartsWith("-t"))
					{
					string tabWidthString = null;
					
					if (command == "-t")
						{
						if (index < commandLine.Length && commandLine[index][0] != '-')
							{  
							tabWidthString = commandLine[index];
							index++;
							}
						}
					else if (command.Length > 2)
						{  tabWidthString = command.Substring(2);  }
						
					int tabWidth;
					
					if (tabWidthString != null && Int32.TryParse(tabWidthString, out tabWidth) && tabWidth > 0)
						{  Engine.Instance.Config.CommandLineConfig.TabWidth = tabWidth;  }						
					else
						{
						errorList.Add( 
							Locale.Get("NaturalDocs.CLI", "CommandLine.TabLengthMustBeGreaterThanZero")
							);
						}
					}
					
					
					
				// No Auto-Group
					
				else if (command == "-nag")
					{
					Engine.Instance.Config.CommandLineConfig.AutoGroup = false;
					}
					
					

				// Style
					
				else if (command == "-s")
					{
					System.Text.StringBuilder style = new System.Text.StringBuilder();
					
					while (index < commandLine.Length && commandLine[index][0] != '-')
						{
						if (style.Length != 0)
							{  style.Append(' ');  }
							
						style.Append(commandLine[index]);
						index++;
						}

					Engine.Instance.Config.CommandLineConfig.StyleName = style.ToString();
					}
					
					

				// Rebuild
				
				else if (command == "-r")
					{
					Engine.Instance.Config.ReparseEverything = true;
					Engine.Instance.Config.RebuildAllOutput = true;
					}
					
				else if (command == "-ro")
					{
					Engine.Instance.Config.RebuildAllOutput = true;
					}
					
					
					
				// Version
				
				else if (command == "-v")
					{
					Console.WriteLine( Engine.Instance.VersionString );
					return ParseCommandLineResult.InformationalExit;
					}
					
					
					
				// Help
				
				else if (command == "-h" || command == "-?")
					{
					Console.WriteLine( 
						Locale.Get("NaturalDocs.CLI", "CommandLine.SyntaxReference(version).multiline", NaturalDocs.Engine.Instance.VersionString) 
						);
					return ParseCommandLineResult.InformationalExit;
					}



				// Everything else

				else
					{
					errorList.Add( 
						Locale.Get("NaturalDocs.CLI", "CommandLine.InvalidParameter(param)", commandLine[index - 1])
						);
					
					// Skip to next parameter.  We don't need to error on every value following it.
					while (index < commandLine.Length && commandLine[index][0] != '-')
						{  index++;  }
					}
				}
				
				
			// Validation
			
			if (String.IsNullOrEmpty( Engine.Instance.Config.ProjectConfigFolder ))
				{
				errorList.Add( 
					Locale.Get("NaturalDocs.CLI", "CommandLine.NoProjectConfigFolder")
					);
				}				
				
				
			// Done.
				
			if (errorList.Count == originalErrorCount)
				{  return ParseCommandLineResult.OK;  }
			else
				{  return ParseCommandLineResult.Error;  }
			}
		
			

		/* Function: HandleException
		 */
		static public void HandleException (Exception e)
			{
			Console.Write ("\n\n------------------------------------------------------------\n");
			Console.WriteLine (Locale.SafeGet("NaturalDocs.CLI", "Crash.Exception",
										"Natural Docs has closed because of the following error:"));
			Console.WriteLine();
			Console.WriteLine(e.Message);
				
			
			// If it's not a user friendly exception or a thread exception wrapping a user friendly exception...
			if ( e.GetType() != typeof(Engine.Exceptions.UserFriendly) &&
				 ( e.GetType() == typeof(Engine.Exceptions.Thread) &&
				   e.InnerException.GetType() == typeof(Engine.Exceptions.UserFriendly) ) == false )
				{
				Engine.Path crashFile = Engine.Instance.BuildCrashReport(e);

				if (crashFile != null)
					{
					Console.WriteLine();
					Console.Write (Locale.SafeGet("NaturalDocs.CLI", "Crash.ReportAt(file).multiline", 
										  "A crash report has been generated at {0}.\n"
										  + "Please include this file when asking for help at naturaldocs.org.\n", crashFile));
					}
					
				else
					{
					Console.WriteLine ();
					Console.WriteLine (e.StackTrace);  
					
					// If it's a thread exception, skip the first inner one because that's the wrapped one, which we already got the
					// message for.
					if (e is Engine.Exceptions.Thread)
						{  e = e.InnerException;  }
						
					while (e.InnerException != null)
						{
						e = e.InnerException;

						Console.WriteLine ();
						Console.WriteLine (Locale.SafeGet("NaturalDocs.CLI", "Crash.NestedException",
													   "This error was caused by the following error:") + "\n");

						Console.WriteLine (e.Message);
						}
						
					try
						{
						Console.WriteLine ();
						Console.WriteLine ( Locale.SafeGet("NaturalDocs.CLI", "Crash.Version", "Version") +
														": " + Engine.Instance.VersionString );
						Console.WriteLine ( Locale.SafeGet("NaturalDocs.CLI", "Crash.Platform", "Platform") +
														": " + Environment.OSVersion.VersionString +
														" (" + Environment.OSVersion.Platform + ")" );
						Console.WriteLine ( "SQLite: " + Engine.SQLite.API.LibVersion() );
						Console.WriteLine ();
						Console.WriteLine ( Locale.SafeGet("NaturalDocs.CLI", "Crash.CommandLine", "Command Line") + ":" );
						Console.WriteLine ();
						Console.WriteLine ("   " + Environment.CommandLine );
						}
					catch
						{
						}
						
					Console.WriteLine ();
					Console.WriteLine (Locale.SafeGet("NaturalDocs.CLI", "Crash.IncludeInfoAndGetHelp",
												   "Please include this information when asking for help at naturaldocs.org."));
					}
				}

			Console.Write ("\n------------------------------------------------------------\n\n");
			}
			
			
		/* Function: HandleErrorList
		 */
		static public void HandleErrorList (Engine.Errors.ErrorList errorList)
			{
			// Annotate any config files before printing them to the console, since the line numbers may change.
			
			Engine.ConfigFile.TryToAnnotateWithErrors(errorList);
			
			
			// Write them to the console.
			
			Console.WriteLine();
			
			Path lastErrorFile = null;
			bool hasNonFileErrors = false;
				
			foreach (Engine.Errors.Error error in errorList)
				{  
				if (error.File != lastErrorFile)
					{
					if (error.File != null)
						{  Console.WriteLine( Locale.Get("NaturalDocs.CLI", "CommandLine.ErrorsInFile(file)", error.File) );  }

					lastErrorFile = error.File;
					}
					
				if (error.File != null)
					{
					Console.Write("   - ");
					
					if (error.LineNumber > 0)
						{  Console.Write( Locale.Get("NaturalDocs.CLI", "CommandLine.Line") + " " + error.LineNumber + ": " );  }
					}
				else
					{  hasNonFileErrors = true;  }
					
				System.Console.WriteLine(error.Message);  
				}
				
			if (hasNonFileErrors == true)
				{  System.Console.WriteLine( Locale.Get("NaturalDocs.CLI", "CommandLine.HowToGetCommandLineRef") );  }
				
			System.Console.WriteLine();
			}
		
		}
	}
