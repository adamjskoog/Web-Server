using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System.IO;

namespace CS422
{
    public abstract class Dir422
    {
        public abstract string Name { get; }
        public abstract IList<Dir422> GetDirs();

        public abstract IList<File422> GetFiles();
        public abstract Dir422 Parent { get; }

        public abstract bool ContainsFile(string fileName, bool recursive);
        public abstract bool ContainsDir(string dirName, bool recursive);

        public abstract File422 GetFile(string fileName);
        public abstract Dir422 GetDir(string dirName);

        public abstract File422 CreateFile(string fileName);
        public abstract Dir422 CreateDir(string dirName);
    }

    public abstract class File422
    {
        public abstract Dir422 Parent { get; }
        public abstract string Name { get; }

        public abstract Stream OpenReadOnly();
        public abstract Stream OpenReadWrite();
    }

    public abstract class FileSys422
    {
        public abstract Dir422 GetRoot();

        public virtual bool Contains(File422 file)
        {
            return Contains(file.Parent);
        }

        public virtual bool Contains(Dir422 dir)
        {
            if (dir == null) { return false; }

            try
            {
                while (dir.Parent != null)
                {
                    dir = dir.Parent;
                }
            }
            catch
            {
                return Object.ReferenceEquals(dir, GetRoot());
            }
            return false;

            
        }

    }

    public class StdFSDir : Dir422
    {
        private string m_path;
        private Dir422 m_parent;

        internal StdFSDir(string path, Dir422 parent)
        {

            m_path = path;
            m_parent = parent;
        }
       
        public string AbsolutePath
        {
            get
            {
                return m_path;
            }
        }

        public override string Name
        {
            get
            {
                return Path.GetFileName(m_path);
            }
        }

        

        public override Dir422 Parent
        {
            get
            {
                return m_parent;
            }
        }

        public override bool ContainsDir(string dirName, bool recursive)
        {
            
            try
            {
                string[] dirs = Directory.GetDirectories(m_path);

                //Recursive approach (requires searching sub directories)
                if (recursive && !dirName.Contains('/') && !dirName.Contains(@"\"))
                {
                    //Base Case: No more directories and no result was found
                    if (dirs == null || dirs.Length == 0)
                    { return false; }
                    
                    //Check for dir name within only the current directory
                    
                    foreach(string s in dirs)
                    {
                        if (Path.GetFileName(s) == dirName)
                            return true;
                    }
                    
                    //Go through the sub directories and search
                    foreach (string s in dirs)
                    {
                        //slice paths for every instantiation
                        StdFSDir dir = new StdFSDir(s, this);
                        if (dir.ContainsDir(dirName, recursive))
                            return true;
                    }

                }
                else if (!dirName.Contains('/') && !dirName.Contains(@"\"))
                {
                    //Check for dir name within only the current directory
                    if (dirs.Contains(dirName))
                    { return true; }
                    
                }
            }
            catch
            {
                return false;
            }
            //Either invalid dir name or dir name was not found
            return false;


        }

        public override bool ContainsFile(string fileName, bool recursive)
        {
            
            string[] dirs = Directory.GetDirectories(m_path);
            string[] files = Directory.GetFiles(m_path);
            //Recursive approach (requires searching sub directories)
            if (recursive && !fileName.Contains('/') && !fileName.Contains(@"\"))
            {
                
                //Base Case: Check for file name within only the current directory
                foreach(string s in files)
                {
                    if (fileName == Path.GetFileName(s)) { return true; }
                }
                
                //No more directories and no result was found
                if (dirs == null || dirs.Length == 0)
                { return false; }

                //Go through the sub directories and search
                foreach (string s in dirs)
                {
                    StdFSDir dir = new StdFSDir(s, this);
                    dir.ContainsDir(fileName, recursive);
                }
            }
            else if (!fileName.Contains('/') && !fileName.Contains(@"\"))
            {
                //Check for dir name within only the current directory
                if (files.Contains(Path.GetFileName(fileName)))
                { return true; }
            }
            //Either invalid dir name or dir name was not found
            return false;
        }

        public override Dir422 CreateDir(string dirName)
        {
            if(!string.IsNullOrEmpty(dirName) && !dirName.Contains('/') && !dirName.Contains(@"\"))
            {
                try
                {
                    string path = m_path + '/' + dirName;

                    // Determine whether the directory exists.
                    if (!Directory.Exists(path))
                    {
                        //Create the directory if it does not exist
                        Directory.CreateDirectory(path);
                    }

                    //Just return that
                    return new StdFSDir(path, this);
                }
                catch (Exception e)
                {
                    Console.WriteLine("The process failed: {0}", e.ToString());
                    return null;
                }
            }
            
            //Invalid string
            return null;
        }

        //Creates a file on the local FS based off this dirs path
        public override File422 CreateFile(string fileName)
        {
            if (!string.IsNullOrEmpty(fileName) && !fileName.Contains('/') && !fileName.Contains(@"\"))
            {
                try
                {
                    string path = m_path + '/' + fileName;

                    //Create the File
                    File.Create(path);
                    return new StdFSFile(path, this);
                }
                catch (Exception e)
                {
                    Console.WriteLine("The process failed: {0}", e.ToString());
                    return null;
                }
            }

            //Invalid string
            return null;
        }

        public override Dir422 GetDir(string dirName)
        {
            //Invalid character check
            if (!string.IsNullOrEmpty(dirName) && !dirName.Contains('/') && !dirName.Contains(@"\"))
            {
                try {
                    string path = m_path + '/' + dirName;
                    //If it does not exist return null
                    if (!Directory.Exists(path))
                        return null;
                    //Create a new dir and return it
                    return new StdFSDir(path, this);

                }
                catch
                {
                    return null;
                }
                
            }
            return null;
        }

        //Returns a list of directories that reside in the current working directory
        public override IList<Dir422> GetDirs()
        {

            //Get all the directories
            try
            {
                string[] directories = Directory.GetDirectories(m_path);
                List<Dir422> dirs = new List<Dir422>();
                //go through the list and add them to a list as children to this directory
                foreach (string s in directories)
                {
                    dirs.Add(new StdFSDir(s, this));
                }

                return dirs;
            }
            catch
            {
                //unauthorized directory access
                return null;
            }
           
            

        }

        //Returns a file provided it exists in the filesystem
        public override File422 GetFile(string fileName)
        {
            //Invalid character check
            if (!string.IsNullOrEmpty(fileName) && !fileName.Contains('/') && !fileName.Contains(@"\"))
            {
                try
                {
                    string path = m_path + '/' + fileName;
                    //Create a new file and return it
                    if (File.Exists(path))
                        return new StdFSFile(path, this);

                }
                catch
                {
                    return null;
                }

            }
            return null;
        }

        public override IList<File422> GetFiles()
        {
            
            //Get all the files
            string[] sysfiles = Directory.GetFiles(m_path);
            List<File422> files = new List<File422>();
            //go through the list and add them to a list as children to this directory
            foreach (string s in sysfiles)
            {
                files.Add(new StdFSFile(Path.GetFileName(s), this));
            }

            return files;

        }
        
    }

    //Represents a standard system file
    public class StdFSFile : File422
    {
        private string m_path;
        private StdFSDir m_parent;

        internal StdFSFile(string path, StdFSDir parent)
        {
            m_path = path;
            m_parent = parent;
        }

        public override string Name
        {
            get
            {
                return Path.GetFileName(m_path);
            }
        }

        public override Dir422 Parent
        {
            get
            {
                return m_parent;
            }
        }

        public override Stream OpenReadOnly()
        {
            try { return new FileStream(m_path, FileMode.Open, FileAccess.Read); }
            catch { return null; }

            
        }

        public override Stream OpenReadWrite()
        {
            try { return new FileStream(m_path, FileMode.Open, FileAccess.ReadWrite); }
            catch { return null; }
        }

    }

    //Represents access to the standard file system provided by the operating system
    public class StandardFileSystem : FileSys422
    {
        private StdFSDir m_root;

        public override Dir422 GetRoot()
        {
            return m_root;
        }

        //Private constructor only can be used in the create function
        private StandardFileSystem(StdFSDir root)
        {
            m_root = root;
        }

        //Create the root of the file system from a given path
        public static StandardFileSystem Create(string rootDir)
        {
           
            if (Directory.Exists(rootDir))
            {
                try
                {
                    return new StandardFileSystem(new StdFSDir(rootDir, null));
                }
                catch(Exception e)
                {
                    Console.WriteLine(e);
                    return null;
                }
                

            }

            //Return null on failure
            return null;
           
        }

    }

    //Represents a filesystem in memory
    public class MemoryFileSystem : FileSys422
    {
        private MemFSDir m_root;

        public MemoryFileSystem()
        {
            m_root = new MemFSDir(@"/",null);
        }

        public override Dir422 GetRoot()
        {
            return m_root;
        }
    }

    public class MemFSDir : Dir422
    {
        private string _name;
        private Dir422 _parent;
        private List<Dir422> _directories;
        private List<File422> _files;

        internal MemFSDir(string name, Dir422 parent)
        {
            _name = name;
            _parent = parent;
            _directories = new List<Dir422>();
            _files = new List<File422>();

        }


        public override string Name
        {
            get
            {
                return _name;
            }
        }

        public override Dir422 Parent
        {
            get
            {
                return _parent;
            }
        }

        public override bool ContainsDir(string dirName, bool recursive)
        {
            try
            {
                //Recursive approach (requires searching sub directories)
                if (recursive && !dirName.Contains('/') && !dirName.Contains(@"\"))
                {
                    if (_directories.Count > 0)
                    {
                        //Check for dir name within only the current directory
                        foreach (Dir422 dir in _directories)
                        {
                            if (dir.Name == dirName)
                            { return true; }

                        }
                        
                        //Go through the sub directories and search
                        foreach (Dir422 dir in _directories)
                        {
                            dir.ContainsDir(dirName, recursive);
                        }

                    }
                }
                else if (!dirName.Contains('/') && !dirName.Contains(@"\"))
                {
                    //Check for dir name within only the current directory
                    foreach (Dir422 dir in _directories)
                    {
                        if (dir.Name == dirName) { return true;}

                    }
                }
            }
            catch
            {
                return false;
            }
            //Either invalid dir name or dir name was not found
            return false;
        }

        public override bool ContainsFile(string fileName, bool recursive)
        {
            try
            {
                if (recursive && !fileName.Contains('/') && !fileName.Contains(@"\"))
                {

                    //Base Case: Check for file name within only the current directory
                    if (_directories.Count > 0)
                    {
                        foreach (File422 file in _files)
                        {
                            if (file.Name == fileName) { return true; }

                        }

                        //search through sub direcetories
                        foreach (Dir422 dir in _directories)
                        {
                            dir.ContainsDir(fileName, recursive);
                        }
                    }


                   
                }
                else if (!fileName.Contains('/') && !fileName.Contains(@"\"))
                {
                    //Check for dir name within only the current directory
                    foreach (File422 file in _files)
                    {
                        if (file.Name == fileName) { return true; }

                    }
                }
            }
            catch
            { }
            
            //Either invalid dir name or dir name was not found
            return false;
        }

        public override Dir422 CreateDir(string dirName)
        {
            if (!string.IsNullOrEmpty(dirName) && !dirName.Contains('/') && !dirName.Contains(@"\"))
            {
                try
                {
                    //First check to see if it exists
                    foreach(Dir422 dir in _directories) { if (dir.Name == dirName) return dir; }
                    
                    //otherwise create a new dir and add
                    MemFSDir temp = new MemFSDir(dirName, this);
                    _directories.Add(temp);
                    return temp;
                }
                catch (Exception e)
                {
                    Console.WriteLine("The process failed: {0}", e.ToString());
                    return null;
                }
            }

            //Invalid string
            return null;
        }

        public override File422 CreateFile(string fileName)
        {
            if (!string.IsNullOrEmpty(fileName) && !fileName.Contains('/') && !fileName.Contains(@"\"))
            {
                try
                {
                    //First check to see if it exists
                    foreach (File422 file in _files) { if (file.Name == fileName) return file; }

                    //otherwise create a new dir and add
                    MemFSFile temp = new MemFSFile(fileName, this);
                    _files.Add(temp);
                    return temp;
                }
                catch (Exception e)
                {
                    Console.WriteLine("The process failed: {0}", e.ToString());
                    return null;
                }
            }

            //Invalid string
            return null;
        }

        public override Dir422 GetDir(string dirName)
        {
            if (!string.IsNullOrEmpty(dirName) && !dirName.Contains('/') && !dirName.Contains(@"\"))
            {
                try
                {   //Search for the directory
                    foreach(Dir422 dir in _directories) { if (dir.Name == dirName) return dir; }

                }
                catch
                {
                    return null;
                }

            }
            return null;
        }

        public override IList<Dir422> GetDirs()
        {
            return _directories;
        }

        public override File422 GetFile(string fileName)
        {
            if (!string.IsNullOrEmpty(fileName) && !fileName.Contains('/') && !fileName.Contains(@"\"))
            {
                try
                {   //Search for the file
                    foreach (File422 file in _files) { if (file.Name == fileName) return file; }

                }
                catch
                {
                    return null;
                }

            }
            return null;
        }

        public override IList<File422> GetFiles()
        {
            return _files;
        }
    }

    public class MemFSFile : File422
    {
        private string _name;
        private Dir422 _parent;
        private bool _writing;
        private int _readCount;
        
        public event EventHandler Writing;
        public event EventHandler Reading;
        

        protected virtual void OnOpenReadWrite()
        {
            //If it is writing then 
            if (Writing == null)
                _writing = true;
            _readCount++;
        
        }


        public MemFSFile(string name, Dir422 parent)
        {
            _name = name;
            _parent = parent;
            _readCount = 0;
            _writing = false;
        }

        public override string Name
        {
            get
            {
                return _name;
            }
        }

        public override Dir422 Parent
        {
            get
            {
                return _parent;
            }
        }

        public override Stream OpenReadOnly()
        {
            if (_writing)
            {
                return null;
            }
            ORWMemStream memStream = new ORWMemStream();
            return new ORWMemStream();
        }

        public override Stream OpenReadWrite()
        {
           if(_writing)
            {
                return null;
            }
            if (_readCount > 0)
            {
                return null;
            }
            return new ORWMemStream();


        }
    }

    public class ORWMemStream : MemoryStream
    {
       

        public override void Close()
        {
            
            base.Close();

        }
    }



}
