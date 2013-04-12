using System.Collections.Generic;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Web;
// using System.Web.Mvc;
using System.Web.Script.Serialization;

//namespace para manipulação de imagens

namespace JqueryFileUpload
{
    /// <summary>
    /// Summary description for Upload
    /// </summary>
    public class Upload : IHttpHandler
    {
        private readonly JavaScriptSerializer js;

        // Diretório onde o arquivo será salvo
        private string StorageRoot { get {
            return Path.Combine(System.Web.HttpContext.Current.Server.MapPath("~/Uploads/"));
        } }

        // metodo de inicialização
        public Upload()
        {
            js = new JavaScriptSerializer();
            js.MaxJsonLength = 41943040;
        }

        public bool IsReusable { get { return false; } }

        // Processa a requisição
        public void ProcessRequest(HttpContext context)
        {
            context.Response.AddHeader("Pragma", "no-cache");
            context.Response.AddHeader("Cache-Control", "private, no-cache");

            // ReturnOptions(context);
            HandleMethod(context);
        }

        // Lidar com pedido baseado no método
        private void HandleMethod(HttpContext context)
        {
            switch (context.Request.HttpMethod)
            {
                case "HEAD":
                case "GET":
                    if (GivenFilename(context)) DeliverFile(context);
                    else ListCurrentFiles(context);
                    break;

                case "POST":
                case "PUT":
                    UploadFile(context);
                    break;

                case "DELETE":
                    DeleteFile(context);
                    break;

                case "OPTIONS":
                    ReturnOptions(context);
                    break;

                default:
                    context.Response.ClearHeaders();
                    context.Response.StatusCode = 405;
                    break;
            }
        }

        // Opções de retorno liberadas
        private static void ReturnOptions(HttpContext context)
        {
            context.Response.AddHeader("Allow", "DELETE,GET,HEAD,POST,PUT,OPTIONS");
            context.Response.StatusCode = 200;
        }

        // Remove Arquivo do Servidor
        private void DeleteFile(HttpContext context)
        {
            var filePath = StorageRoot + context.Request["f"];
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }

        private void UploadFile(HttpContext context)
        {
            var statuses = new List<FilesStatus>();
            var headers = context.Request.Headers;

            if (string.IsNullOrEmpty(headers["X-File-Name"]))
            {
                UploadWholeFile(context, statuses);
            }
            else
            {
                UploadPartialFile(headers["X-File-Name"], context, statuses);
            }

            WriteJsonIframeSafe(context, statuses);
        }

        // Enviar arquivo parcial
        private void UploadPartialFile(string fileName, HttpContext context, List<FilesStatus> statuses)
        {
            if (context.Request.Files.Count != 1) throw new HttpRequestValidationException("Attempt to upload chunked file containing more than one fragment per request");
            var inputStream = context.Request.Files[0].InputStream;
            var fullName = StorageRoot + Path.GetFileName(fileName);

            using (var fs = new FileStream(fullName, FileMode.Append, FileAccess.Write))
            {
                var buffer = new byte[1024];

                var l = inputStream.Read(buffer, 0, 1024);
                while (l > 0)
                {
                    fs.Write(buffer, 0, l);
                    l = inputStream.Read(buffer, 0, 1024);
                }
                fs.Flush();
                fs.Close();
            }
            statuses.Add(new FilesStatus(new FileInfo(fullName)));
        }

        // Envia arquivo completo
        private void UploadWholeFile(HttpContext context, List<FilesStatus> statuses)
        {
            // efetua loop na lista de arquivos
            for (int i = 0; i < context.Request.Files.Count; i++)
            {
                // Pega o arquivo
                var file = context.Request.Files[i];

                // Aqui você personaliza o nome do arquivo que será salvo
                var imgName = "__" + Path.GetFileName(file.FileName);

                // Aqui você personaliza as dimensões
                int imgHeight = 50;
                int imgWidth = 150;

                // Define o caminho completo onde o arquivo irá ser salvo (Diretório + Nome do Arquivo)
                var fullPath = StorageRoot + imgName;

                // Manipula a imagem aplicando um fundo branco e redimensonando a mesma
                Stream imgStream = file.InputStream;
                Bitmap newFile = ResizeImage(imgStream, imgWidth, imgHeight);

                // Salva a imagem em formato Jpeg
                newFile.Save(fullPath, System.Drawing.Imaging.ImageFormat.Jpeg);
                statuses.Add(new FilesStatus(imgName, file.ContentLength, fullPath));
            }
        }

        // Retorna o JSON
        private void WriteJsonIframeSafe(HttpContext context, List<FilesStatus> statuses)
        {
            context.Response.AddHeader("Vary", "Accept");
            try
            {
                if (context.Request["HTTP_ACCEPT"].Contains("application/json"))
                    context.Response.ContentType = "application/json";
                else
                    context.Response.ContentType = "text/plain";
            }
            catch
            {
                context.Response.ContentType = "text/plain";
            }

            var uploadedFiles = new { files = statuses.ToArray() };
            var jsonObj = js.Serialize(uploadedFiles);
            context.Response.Write(jsonObj);
        }

        private static bool GivenFilename(HttpContext context)
        {
            return !string.IsNullOrEmpty(context.Request["f"]);
        }

        private void DeliverFile(HttpContext context)
        {
            var filename = context.Request["f"];
            var filePath = StorageRoot + filename;

            if (File.Exists(filePath))
            {
                context.Response.AddHeader("Content-Disposition", "attachment; filename=\"" + filename + "\"");
                context.Response.ContentType = "application/octet-stream";
                context.Response.ClearContent();
                context.Response.WriteFile(filePath);
            }
            else
                context.Response.StatusCode = 404;
        }

        private void ListCurrentFiles(HttpContext context)
        {
            var files =
                new DirectoryInfo(StorageRoot)
                    .GetFiles("*", SearchOption.TopDirectoryOnly)
                    .Where(f => !f.Attributes.HasFlag(FileAttributes.Hidden))
                    .Select(f => new FilesStatus(f))
                    .ToArray();

            var uploadedFiles = new { files = files.ToArray() };
            string jsonObj = js.Serialize(uploadedFiles);
            context.Response.AddHeader("Content-Disposition", "inline; filename=\"files.json\"");
            context.Response.Write(jsonObj);
            context.Response.ContentType = "application/json";
        }

        // Redimensionamento da imagem
        private Bitmap ResizeImage(Stream imgStream, int? newWidth, int? newHeight)
        {
            Bitmap imgOut = null;

            // fallback dos tamanhos das imagens
            if (newHeight == null)
                newHeight = 800;
            if (newWidth == null)
                newWidth = 600;

            try
            {
                Bitmap loBMP = new Bitmap(imgStream);
                ImageFormat loFormat = loBMP.RawFormat;

                //*** Se a imagem for menor que a nova retorna a nova
                if (loBMP.Width < newWidth && loBMP.Height < newHeight)
                {
                    return loBMP;
                }

                imgOut = new Bitmap((int)newWidth, (int)newHeight);
                Graphics g = Graphics.FromImage(imgOut);
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.FillRectangle(Brushes.White, 0, 0, (int)newWidth, (int)newHeight);
                g.DrawImage(loBMP, 0, 0, (int)newWidth, (int)newHeight);
                loBMP.Dispose();
            }
            catch (Exception)
            {
                return null;
            }

            return imgOut;
        }
    }
}