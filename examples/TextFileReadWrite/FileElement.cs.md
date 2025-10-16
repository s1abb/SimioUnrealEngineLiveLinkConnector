```c#
using System;
using System.Collections.Generic;
using System.Text;
using SimioAPI;
using SimioAPI.Extensions;

namespace CustomSimioStep
{
    class FileElementDefinition : IElementDefinition
    {
        #region IElementDefinition Members

        /// <summary>
        /// Property returning the full name for this type of element. The name should contain no spaces. 
        /// </summary>
        public string Name
        {
            get { return "MyTextFile"; }
        }

        /// <summary>
        /// Property returning a short description of what the element does.  
        /// </summary>
        public string Description
        {
            get { return "Used with ReadText and WriteText steps.\nThe File element is used in conjunction with the user defined Read and Write steps to read and write to an external file."; }
        }

        /// <summary>
        /// Property returning an icon to display for the element in the UI. 
        /// </summary>
        public System.Drawing.Image Icon
        {
            get { return null; }
        }

        /// <summary>
        /// Property returning a unique static GUID for the element.  
        /// </summary>
        public Guid UniqueID
        {
            get { return MY_ID; }
        }
        // We need to use this ID in the element reference property of the Read/Write steps, so we make it public
        public static readonly Guid MY_ID = new Guid("{29F7FF04-3EC6-4EDC-A614-1DED3DF1A9F8}"); //Jan2024/danH

        /// <summary>
        /// Method called that defines the property, state, as well as event schema for the element.
        /// </summary>
        public void DefineSchema(IElementSchema schema)
        {
            IPropertyDefinition pd = schema.PropertyDefinitions.AddStringProperty("FilePath", String.Empty);
            pd.Description = "The name of the text file that is being read from or written to.";

        }

        /// <summary>
        /// Method called to add a new instance of this element type to a model. 
        /// Returns an instance of the class implementing the IElement interface.
        /// </summary>
        public IElement CreateElement(IElementData data)
        {
            return new FileElement(data);
        }

        #endregion
    }

    /// <summary>
    /// Element containing information about a file, 
    /// as well as open it at runtime.
    /// </summary>
    class FileElement : IElement, IDisposable
    {
        IElementData _data;
        string _writerFilePath;
        string _readerFilePath;

        string _filePath = null;

        /// <summary>
        /// This is done so we have access to it by other classes, such as the Read and Write Steps.
        /// </summary>
        public string FilePath { get { return _filePath; } set { _filePath = value; } }

        public FileElement(IElementData data)
        {
            _data = data;
            IPropertyReader _prFilepath = _data.Properties.GetProperty("FilePath");

            // Cache the names of the files to open for reading or writing
            if ( FilePath == null ) 
            {
                FilePath = _prFilepath.GetStringValue(_data.ExecutionContext);
            }

            if ( String.IsNullOrEmpty(FilePath) == false)
            {
                string fileRoot = null;
                string fileDirectoryName = null;

                try
                {
                    fileRoot = System.IO.Path.GetPathRoot(FilePath);
                    fileDirectoryName = System.IO.Path.GetDirectoryName(FilePath);
                }
                catch (ArgumentException ex)
                {
                    data.ExecutionContext.ExecutionInformation.ReportError($"Failed to create runtime file element. Filepath={FilePath}. Err={ex.Message}");
                }

                // Left these here as comments to show how other potential filenames could be constructed
                ////string simioExperimentName = _data.ExecutionContext.ExecutionInformation.ExperimentName;
                ////string simioScenarioName = _data.ExecutionContext.ExecutionInformation.ScenarioName;
                ////string simioReplicationNumber = _data.ExecutionContext.ExecutionInformation.ReplicationNumber.ToString();

                // If missing directory or root, then set directory to Simio project folder.
                if (String.IsNullOrEmpty(fileDirectoryName) || String.IsNullOrEmpty(fileRoot))
                {
                    string simioProjectFolder = _data.ExecutionContext.ExecutionInformation.ProjectFolder;

                    fileDirectoryName = simioProjectFolder;
                    FilePath = $@"{fileDirectoryName}\{FilePath}";
                }

                _readerFilePath = FilePath;
                _writerFilePath = FilePath;
            }
        }

        System.IO.TextWriter _writer;
        public System.IO.TextWriter Writer
        {
            get
            {
                // If there is already reader for this file, then we cannot write.
                if (_reader != null)
                {
                    _data.ExecutionContext.ExecutionInformation.ReportError($"Trying to write to {_writerFilePath}, which is already open for reading.");
                    return null;
                }

                // If we don't already have a writer, create one
                try
                {
                    if (String.IsNullOrEmpty(_writerFilePath))
                        ReportFileOpenError("[No file specified]", "writing", "[None]");
                    else if (_writer == null)
                        _writer = new System.IO.StreamWriter(_writerFilePath);
                }
                catch (Exception e)
                {
                    _writer = null;
                    ReportFileOpenError(_writerFilePath, "writing", e.Message);
                }

                return _writer;
            }
        }

        System.IO.TextReader _reader;
        public System.IO.TextReader Reader
        {
            get
            {
                // We can't read and write at the same time
                if (_writer != null)
                {
                    _data.ExecutionContext.ExecutionInformation.ReportError($"Trying to read from {_readerFilePath}, which is already open for writing." ?? "[No file specified]");
                    return null;
                }

                // If we don't already have a reader, create one
                try
                {
                    if (String.IsNullOrEmpty(_readerFilePath))
                        ReportFileOpenError("[No file specified]", "reading", "[None]");
                    if (_reader == null)
                        _reader = new System.IO.StreamReader(_readerFilePath);
                }
                catch (Exception e)
                {
                    _reader = null;
                    ReportFileOpenError(_readerFilePath, "reading", e.Message);
                }

                return _reader;
            }
        }

        void ReportFileOpenError(string fileName, string action, string exceptionMessage)
        {
            _data.ExecutionContext.ExecutionInformation.ReportError($"Error opening {fileName} for {action}. This may mean the specified file, path or disk does not exist.\n\nInternal exception message: {exceptionMessage}");
        }

        #region IElement Members

        /// <summary>
        /// No initialization logic needed, we will open the file on the first read or write request
        /// </summary>
        public void Initialize()
        { 
        }

        /// <summary>
        /// Method called when the simulation run is terminating.
        /// We'll close the file here.
        /// </summary>
        public void Shutdown()
        {
            if (_writer != null)
            {
                try
                {
                    _writer.Close();
                    _writer.Dispose();
                }
                catch(Exception e)
                {
                    _data.ExecutionContext.ExecutionInformation.ReportError($"There was a problem closing file '{_writerFilePath ?? String.Empty}' for writing. Message: {e.Message}");
                }
                finally
                {
                    _writer = null;
                }
            }

            if (_reader != null)
            {
                try
                {
                    _reader.Close();
                    _reader.Dispose();
                }
                catch(Exception e)
                {
                    _data.ExecutionContext.ExecutionInformation.ReportError($"There was a problem closing file '{_readerFilePath ?? String.Empty}' for reading. Message: {e.Message}");
                }
                finally
                {
                    _reader = null;
                }
            }
        }

        #endregion

        #region IDisposable Members

        public void Dispose()
        {
            Shutdown();
        }

        #endregion
    }  // FileElement
}
```