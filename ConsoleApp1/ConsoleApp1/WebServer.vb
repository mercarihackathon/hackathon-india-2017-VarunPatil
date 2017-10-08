Imports System.Net
Imports System.Text
Imports System.Threading

Namespace SimpleWebserver
    Class StartSimpleWebserver
        Public Shared Sub start()
            ThreadPool.QueueUserWorkItem(Sub()
                                             Dim serv As New AsyncServer
                                         End Sub)
        End Sub
    End Class

    Partial Public Class AsyncServer
        Public Const NOT_FOUND_MESSAGE As String = "NOT FOUND"

        Public Sub New()
            RefillLocalDataset()
            Dim listener As HttpListener = New HttpListener
            listener.Prefixes.Add("http://localhost:8081/")
            listener.Prefixes.Add("http://127.0.0.1:8081/")

            Using identity As System.Security.Principal.WindowsIdentity = System.Security.Principal.WindowsIdentity.GetCurrent()
                Dim principal As System.Security.Principal.WindowsPrincipal = New System.Security.Principal.WindowsPrincipal(identity)
                If principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator) Then listener.Prefixes.Add("http://+:8081/")
            End Using

            listener.Start()

            While True
                Try
                    Dim context As HttpListenerContext = listener.GetContext()
                    ThreadPool.QueueUserWorkItem(Sub(o) HandleRequest(context))
                Catch
                End Try
            End While
        End Sub

        Dim Cookies As New Dictionary(Of String, String)
        Dim whitelistedfiles As String() = {"frmLogin.html", "frmLogin.js", "formstyle1.css", "common.js",
            "jquery.tablesorter.min.js", "jquery.min.js", "moment.min.js", "jquery.tablesorter.min.js", "moment-precise-range.js"}

        Public Shared Function RandomString(length As Integer) As String
            Dim r As New Random
            Const chars As String = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789abcdefghijklmnopqrstuvwxyz"
            Return New String(Enumerable.Repeat(chars, length).Select(Function(s) s(r.Next(s.Length))).ToArray())
        End Function

        Private Function GetNewCookie() As String
            Return Convert.ToBase64String(Encoding.ASCII.GetBytes((Now.ToShortDateString + Now.ToShortTimeString).Substring(0, 12) + RandomString(25)))
        End Function

        Private Function IsValidCookie(Cookie As String) As Boolean
            Return Cookie.Length > 25
        End Function

        Private Sub Respond_Error(context As HttpListenerContext, response As Integer)
            Dim write As String = ""
            Select Case response
                Case HttpStatusCode.BadRequest
                    write = "Bad Request"
                Case HttpStatusCode.Unauthorized
                    write = "Unauthorized"
                Case HttpStatusCode.ServiceUnavailable
                    write = "Service Unavailable"
                Case HttpStatusCode.Forbidden
                    write = "Forbidden"
                Case HttpStatusCode.NotFound
                    write = "Not Found"
                Case HttpStatusCode.NotImplemented
                    write = "Not Implemented"
                Case HttpStatusCode.InternalServerError
                    write = "Internal Server Error"
            End Select
            context.Response.StatusCode = response
            Dim bytes As Byte() = Encoding.UTF8.GetBytes(CStr(response) + " " + write)
            context.Response.OutputStream.Write(bytes, 0, bytes.Length)
            context.Response.OutputStream.Close()
        End Sub

        Private Sub HandleRequest(state As Object)
            Dim context As HttpListenerContext = DirectCast(state, HttpListenerContext)
            Try
                Dim receivedCookie As Cookie = context.Request.Cookies("sessionid")
                Dim authorized As Boolean = Debugger.IsAttached OrElse receivedCookie IsNot Nothing AndAlso Cookies.ContainsKey(receivedCookie.Value) AndAlso Cookies(receivedCookie.Value) <> String.Empty AndAlso IsValidCookie(receivedCookie.Value)

                If Not authorized Then
                    If Not whitelistedfiles.Contains(context.Request.Url.AbsolutePath.Substring(1)) Then
                        If context.Request.Url.AbsolutePath = "/" OrElse context.Request.Url.AbsolutePath.Contains(".html") Then
                            context.Response.Redirect("/frmLogin.html")
                            context.Response.Close()
                            Exit Sub
                        Else
                            Respond_Error(context, HttpStatusCode.Unauthorized)
                            Exit Sub
                        End If
                    Else
                        If context.Request.Url.AbsolutePath = "/frmLogin.html" AndAlso context.Request.QueryString.Count > 0 Then
                            If context.Request.QueryString("user") Is Nothing OrElse context.Request.QueryString("user") = "" OrElse
                                   context.Request.QueryString("password") Is Nothing OrElse context.Request.QueryString("password") = "" Then
                                Respond_Error(context, HttpStatusCode.BadRequest)
                                Exit Sub
                            Else
                                Dim drs As DataRow() = LocalDataset.UserDetails.Select("UserID='" + context.Request.QueryString("user") + "'")
                                If drs.Count = 0 Then
                                    Respond_Error(context, HttpStatusCode.Unauthorized)
                                    Exit Sub
                                Else
                                    If dbCStr(drs(0)("Password")) = context.Request.QueryString("password") Then
                                        Dim newCookie As String = GetNewCookie()
                                        Dim sendCookie As New Cookie("sessionid", newCookie)
                                        context.Response.AppendCookie(sendCookie)
                                        Cookies(newCookie) = dbCStr(drs(0)("UserID"))
                                        context.Response.StatusCode = HttpStatusCode.OK
                                        Dim bytes As Byte() = Encoding.UTF8.GetBytes("Logged in succesfully")
                                        context.Response.OutputStream.Write(bytes, 0, bytes.Length)
                                        context.Response.OutputStream.Close()
                                        Exit Sub
                                    Else
                                        Respond_Error(context, HttpStatusCode.Forbidden)
                                        Exit Sub
                                    End If
                                End If
                            End If
                        End If
                    End If
                End If

                If authorized Then
                    If context.Request.Url.AbsolutePath = "/logout" Then
                        Cookies.Remove(receivedCookie.Value)
                        context.Response.Redirect("/frmLogin.html")
                        context.Response.Close()
                        Exit Sub
                    End If

                    If context.Request.Url.AbsolutePath = "/frmLogin.html" Then
                        context.Response.Redirect("/frmMain.html")
                        context.Response.Close()
                        Exit Sub
                    End If
                End If

                If context.Request.Url.AbsolutePath = "/" Then
                    context.Response.Redirect("/frmMain.html")
                    context.Response.Close()
                    Exit Sub
                End If

                Dim URLPath As String() = context.Request.Url.AbsolutePath.Split(CChar("/"))
                context.Response.SendChunked = True

                Dim FILE_NAME As String = PededatConstants.HTML_FOLDER_PATH + "Gen"

                If URLPath.Count >= 3 AndAlso URLPath(1) = "get" Then
                    If URLPath(2) = "data" Then
                        If context.Request.QueryString.Count = 0 Then
                            context.Response.StatusCode = HttpStatusCode.NotFound
                            context.Response.Close()
                        End If
                        FILE_NAME = context.Request.QueryString(0)
                        If FILE_NAME(0) = "/" OrElse FILE_NAME(0) = "\" Then FILE_NAME = "." + FILE_NAME
                    Else
                        FILE_NAME += "/" + URLPath(3)
                    End If
                Else
                    FILE_NAME += context.Request.Url.AbsolutePath
                End If

                If IO.File.Exists(FILE_NAME) Then
                    WriteFile(DirectCast(context, HttpListenerContext), FILE_NAME, True)
                Else
                    If authorized Then
                        Dim bytes As Byte() = Encoding.UTF8.GetBytes(HandleNonFileRequest(context))
                        context.Response.OutputStream.Write(bytes, 0, bytes.Length)
                    Else
                        context.Response.StatusCode = HttpStatusCode.Unauthorized
                        Dim bytes As Byte() = Encoding.UTF8.GetBytes("401 Unauthorized")
                        context.Response.OutputStream.Write(bytes, 0, bytes.Length)
                    End If
                End If
                context.Response.Close()

            Catch e As Exception
                Try
                    context.Response.StatusCode = HttpStatusCode.InternalServerError
                    Dim bytes As Byte() = Encoding.UTF8.GetBytes("Error : " + e.Message + " at " + e.TargetSite.Name + Environment.NewLine + e.StackTrace)
                    context.Response.OutputStream.Write(bytes, 0, bytes.Length)
                    context.Response.OutputStream.Close()
                Catch ignored As Exception
                End Try
            End Try
        End Sub

        Public Sub WriteFile(ctx As HttpListenerContext, path__1 As String, html As Boolean)
            Dim response As HttpListenerResponse = ctx.Response
            Using fs As IO.FileStream = IO.File.OpenRead(path__1)
                Dim filename As String = IO.Path.GetFileName(path__1)
                response.ContentLength64 = fs.Length
                response.ContentType = Web.MimeMapping.GetMimeMapping(path__1)

                If Not html Then
                    response.SendChunked = False
                    response.ContentType = System.Net.Mime.MediaTypeNames.Application.Octet
                    response.AddHeader("Content-disposition", Convert.ToString("attachment; filename=") & filename)
                End If

                response.AddHeader("Date", DateTime.Now.ToString("r"))
                response.AddHeader("Last-Modified", IO.File.GetLastWriteTime(path__1).ToUniversalTime.ToString("r"))

                Dim buffer As Byte() = New Byte(64 * 1024 - 1) {}
                Dim read As Integer
                Using bw As New IO.BinaryWriter(response.OutputStream)
                    While (WiFiImage.InlineAssignHelper(read, fs.Read(buffer, 0, buffer.Length))) > 0
                        bw.Write(buffer, 0, read)
                        bw.Flush()
                    End While
                    bw.Close()
                End Using

                response.StatusCode = HttpStatusCode.OK
                response.StatusDescription = "OK"
                response.OutputStream.Close()
            End Using
        End Sub

        Public Function HandleNonFileRequest(context As HttpListenerContext) As String
            Try
                context.Response.StatusCode = HttpStatusCode.OK
                Dim URLPath As String() = context.Request.Url.AbsolutePath.Split(CChar("/"))
                Dim PID As Integer = dbCInt(context.Request.QueryString("pid"))

                Select Case URLPath(1)
                    Case "get"
                        Return handleGetTop(context, URLPath, PID)
                    Case "post"
                        Return handlePostTop(context, URLPath)
                    Case "postxml"
                        Return handlePostXMLTop(context, URLPath, PID)
                End Select

                context.Response.StatusCode = HttpStatusCode.NotFound
                Return NOT_FOUND_MESSAGE
            Catch e As Exception
                context.Response.StatusCode = HttpStatusCode.InternalServerError
                Return "Error : " + e.Message + " at " + e.TargetSite.Name + Environment.NewLine + e.StackTrace
            End Try
        End Function

        Public Function handleGetTop(context As HttpListenerContext, URLPath As String(), pid As Integer) As String
            Select Case URLPath(2)
                Case "report"
                    context.Response.ContentType = "text/html"
                    Return getReport(URLPath(3), pid)
                Case "datatable"
                    context.Response.ContentType = "text/xml"
                    Return getDataTable(URLPath(3), context, pid)
                Case "list"
                    context.Response.ContentType = "text/xml"
                    Dim query As String = URLPath(3).TrimEnd(".xml".ToCharArray)
                    If LastChanged.ContainsKey(query) Then
                        context.Response.AddHeader("Date", DateTime.UtcNow.ToString("r"))
                        context.Response.AddHeader("Last-Modified", LastChanged(query))
                    End If
                    Return getList(query, context)
                Case "value"
                    context.Response.ContentType = "text/plain"
                    Return getValue(URLPath(3), pid)
            End Select
            Return NOT_FOUND_MESSAGE
        End Function

        Public Function handlePostTop(context As HttpListenerContext, URLPath As String()) As String
            context.Response.ContentType = "text/plain"
            Dim QName As String = context.Request.QueryString("name")
            Select Case URLPath(2)
                Case "CaseSheetOPD"
                    Return Update_CaseSheet(context.Request.QueryString)
                Case "BirthRecord"
                    Return Update_BirthRecord(context.Request.QueryString)
                Case "RefDoctorList"
                    Return CStr(Add_RefDoctor(QName))
                Case "HospitalList"
                    Return CStr(Add_Hospital(QName))
                Case "DiagnosisList"
                    Return CStr(Add_Diagnosis(QName))
                Case "CityList"
                    Return CStr(Add_City(QName))
                Case "AtPostList"
                    Return CStr(Add_AtPost(QName, context.Request.QueryString("city")))
                Case "StreetList"
                    Return CStr(Add_Street(QName, context.Request.QueryString("atpost")))
            End Select
            Return NOT_FOUND_MESSAGE
        End Function

        Public Function handlePostXMLTop(context As HttpListenerContext, URLPath As String(), PID As Integer) As String
            context.Response.ContentType = "text/plain"
            Select Case URLPath(2)
                Case "Anthropometry"
                    Return Update_Anthropometry_Grid(context.Request.QueryString("xml"), PID)
                Case "ClinicalHistory"
                    Return Update_ClinicalHistory_Grid(context.Request.QueryString("xml"), PID)
                Case "AnthropometryClinical"
                    Update_Anthropometry_Grid(context.Request.QueryString("anthropometry"), PID)
                    Return Update_ClinicalHistory_Grid(context.Request.QueryString("clinicalhistory"), PID)
                Case "Images"
                    Return Update_Images_Grid(context.Request.QueryString("xml"), PID)
                Case "Investigation"
                    Return Update_Investigation_Grid(context.Request.QueryString("xml"), PID)
                Case "InvestigationList"
                    Return Update_InvestigationList_Grid(context.Request.QueryString("xml"))
            End Select
            Return NOT_FOUND_MESSAGE
        End Function

        Public Function getReport(report As String, Optional pid As Integer = 0) As String
            Select Case report
                Case "summary"
                    Return ReportGenerator.GenerateRegexSummaryReport(pid, True)
                Case "fee-individual"
                    Return ReportGenerator.GenerateRegexSimpleReport(FeeTA.GetDataByPID(pid), PededatConstants.HTML_FOLDER_PATH + "template-feereport-individual.html", pid, True, Nothing, Nothing, True)
                Case "clinical-history"
                    Return ReportGenerator.GenerateRegexSimpleReport(ClinicalHistoryTA.GetDataByPID(pid), PededatConstants.HTML_FOLDER_PATH + "template-clinical-regex.html", pid, True, Nothing, Nothing, True)
            End Select
            Return NOT_FOUND_MESSAGE
        End Function

        Public Function getDataTable(TableName As String, context As HttpListenerContext, Optional pid As Integer = 0) As String
            Select Case TableName
                Case "Profile"
                    Dim drs As DataRow() = LocalDataset.Profile.Select("PatientID='" + CStr(pid) + "'")
                    If drs.Count > 0 Then
                        Return getXMLDataTableFromDataRows(drs, LocalDataset.Profile)
                    Else
                        context.Response.StatusCode = HttpStatusCode.NotFound
                        Return "Not Found"
                    End If
                Case "Anthropometry"
                    Return getXMLDataTable(AnthropometryTA.GetDataByPID(pid))
                Case "BirthRecord"
                    Return getXMLDataTable(BirthRecordOpdTA.GetDataByPID(pid))
                Case "ClinicalHistory"
                    Return getXMLDataTable(ClinicalHistoryTA.GetDataByPID(pid))
                Case "Fee"
                    Return getXMLDataTable(FeeTA.GetDataByPID(pid))
                Case "SearchOPD"
                    Return GetSearchOPD(context)
                Case "Images"
                    Return getXMLDataTable(ImagesTA.GetDataByPID(pid))
                Case "Investigation"
                    Return getXMLDataTable(PrescriptionTA.GetDataByPIDInvest(pid))
                Case "InvestigationList"
                    Return getXMLDataTable(LocalDataset.InvestigationList)
            End Select
            Return NOT_FOUND_MESSAGE
        End Function

        Public Function getList(query As String, context As HttpListenerContext) As String
            Select Case query
                Case "City"
                    Return getListfromDataTable(LocalDataset.City, "City")
                Case "RefDoctor"
                    Return getListfromDataTable(LocalDataset.RefDoctor, "Name")
                Case "HospitalList"
                    Return getListfromDataTable(LocalDataset.HospitalList, "HospitalName")
                Case "DiagnosisList"
                    Return getListfromDataTable(LocalDataset.DiagnosisList, "Name")
                Case "AtPost"
                    Return getListfromDataRows(LocalDataset.AtPost.Select("City='" + context.Request.QueryString("city") + "'"), "AtPost", LocalDataset.AtPost)
                Case "Street"
                    Return getListfromDataRows(LocalDataset.Street.Select("AtPost='" + context.Request.QueryString("atpost") + "'"), "Street", LocalDataset.Street)
            End Select
            Return NOT_FOUND_MESSAGE
        End Function

        Public Function getValue(query As String, Optional pid As Integer = 0) As String
            Select Case query
                Case "totalbalance"
                    Return CStr(dbCInt(FeeTA.GetDataByPID(pid).Compute("SUM(Balance)", "")))
            End Select
            Return NOT_FOUND_MESSAGE
        End Function

        Public Function getTokenDictionary(data As String()) As Dictionary(Of String, String)
            Dim ans As New Dictionary(Of String, String)
            For Each item As String In data
                Dim tokens As String() = item.Split(New Char() {"="c})
                If tokens.Length < 2 Then Continue For
                ans(tokens(0)) = tokens(1)
            Next
            Return ans
        End Function

        Public Function GetDataTableFromXML(data As String, dtf As DataTable, identifiers As Dictionary(Of String, String)) As DataTable
            Dim dt As DataTable = dtf.Clone()
            Dim deletedIndices As New List(Of String)
            Dim pkey As String = dtf.PrimaryKey(0).ColumnName
            data = WebUtility.UrlDecode(data)

            Dim doc As Xml.XmlDocument = New Xml.XmlDocument()
            doc.LoadXml(data)
            For Each TableNode As Xml.XmlNode In doc.FirstChild.ChildNodes
                Dim dr As DataRow = dt.NewRow
                Dim deleted As Boolean = False
                For Each FieldNode As Xml.XmlNode In TableNode.ChildNodes
                    If FieldNode.Name = "delete" Then
                        deleted = True
                        Continue For
                    End If
                    dr(Web.HttpUtility.HtmlDecode(Xml.XmlConvert.DecodeName(FieldNode.Name))) = Web.HttpUtility.HtmlDecode(FieldNode.InnerText)
                Next
                For Each key As String In identifiers.Keys
                    dr(key) = identifiers(key)
                Next
                dt.Rows.Add(dr)
                If deleted Then deletedIndices.Add(CStr(dr(pkey)))
            Next
            dtf.Merge(dt)

            For i As Integer = 0 To dtf.Rows.Count - 1
                If deletedIndices.Contains(CStr(dtf(i)(pkey))) Then dtf(i).Delete()
            Next

            Return dtf
        End Function

        Public Function GetFormSingleRowDataTableFromXML(data As String, dtf As DataTable, ByRef Optional rejected As Dictionary(Of String, String) = Nothing) As DataTable
            Dim dt As DataTable = dtf.Clone
            Dim doc As Xml.XmlDocument = New Xml.XmlDocument()
            Dim dr As DataRow = dt.NewRow
            doc.LoadXml(WebUtility.UrlDecode(data))
            For Each FieldNode As Xml.XmlNode In doc.DocumentElement.ChildNodes
                If dtf.Columns.Contains(FieldNode.Name) Then
                    If IsNumericType(dtf.Columns(Xml.XmlConvert.DecodeName(FieldNode.Name)).DataType) AndAlso Not IsNumeric(FieldNode.InnerText) Then Continue For
                    If dtf.Columns(Xml.XmlConvert.DecodeName(FieldNode.Name)).DataType = GetType(DateTime) AndAlso Not IsDate(FieldNode.InnerText) Then Continue For
                    dr(Xml.XmlConvert.DecodeName(FieldNode.Name)) = FieldNode.InnerText
                Else
                    If rejected IsNot Nothing Then
                        rejected(FieldNode.Name) = FieldNode.InnerText
                    End If
                End If
            Next
            For Each col As DataColumn In dt.Columns
                If (col.AllowDBNull = False) AndAlso IsDBNull(dr(col)) = True Then dr(col) = 0
            Next
            dt.Rows.Add(dr)
            Return dt
        End Function

        Public Function getListfromDataTable(dt As DataTable, col As String) As String
            Return getXMLDataTable(New DataView(dt).ToTable(False, col))
        End Function

        Public Function getListfromDataRows(datarows As DataRow(), col As String, template As DataTable) As String
            Return getXMLDataTable(New DataView(DataRowsToDatatable(datarows, template)).ToTable(False, col))
        End Function

        Public Function getXMLDataTableFromDataRows(datarows As DataRow(), template As DataTable, Optional trim As Integer = 999999) As String
            Return getXMLDataTable(DataRowsToDatatable(datarows, template, trim))
        End Function

        Public Function DataRowsToDatatable(datarows As DataRow(), template As DataTable, Optional trim As Integer = 999999) As DataTable
            Dim i As Integer = 0
            Dim dt As DataTable = template.Clone
            For Each row As DataRow In datarows
                dt.ImportRow(row)
                i += 1
                If i > trim Then Exit For
            Next
            Return dt
        End Function

        Public Function getXMLDataTable(dt As DataTable) As String
            Dim Str As New IO.MemoryStream
            dt.WriteXml(Str, True)
            Str.Seek(0, IO.SeekOrigin.Begin)
            Dim SR As IO.StreamReader = New IO.StreamReader(Str)
            Dim xmlstr As String
            xmlstr = SR.ReadToEnd()
            Return (xmlstr)
        End Function

    End Class
End Namespace
