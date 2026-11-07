using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

/// <summary>Проверка целостности и атомарная замена файла; последняя версия остаётся в .bak.</summary>
public static class SaveFileStore
{
    public static string Digest(string value)
    {using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-","");}
    public static void Write(string path,string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        string temp=path+".tmp";
        using(var stream=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None))
        {byte[] bytes=Encoding.UTF8.GetBytes(text);stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
        if(File.Exists(path))File.Replace(temp,path,path+".bak");else File.Move(temp,path);
    }
}
