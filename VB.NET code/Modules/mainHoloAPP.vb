﻿Imports System.Threading
Public Module mainHoloAPP
    Private inpCommand As String
    Private serverMonitor As New Thread(AddressOf monitorServer)
    Sub Main()
        Console.Title = "Holograph Emulator"
        mainHoloAPP.printHoloProps()

        While True
            inpCommand = Console.ReadLine
            If Not (inpCommand = vbNullString) Then mainHoloAPP.checkCommand(inpCommand)
        End While
    End Sub
    Private Sub printHoloProps()
        Console.WriteLine("HOLOGRAPH***********************************************************")
        Console.WriteLine("THE FREE OPEN-SOURCE HABBO HOTEL EMULATOR")
        Console.WriteLine("FOR MORE DETAILS CHECK LEGAL.TXT")
        Console.WriteLine("COPYRIGHT (C) 2007-2008 BY HOLOGRAPH TEAM")
        Console.WriteLine(vbNullString)
        Console.WriteLine("VERSION:")
        Console.WriteLine(" CORE: V" & My.Application.Info.Version.Major)
        Console.WriteLine(" MAJOR FUNCTIONS: LIB " & My.Application.Info.Version.Minor)
        Console.WriteLine(" REVISION: R" & My.Application.Info.Version.Revision)
        Console.WriteLine(" CLIENT: V18")
        Console.WriteLine(" RELEASE TYPE: TRUNK, .NET release")
        Console.WriteLine(vbNullString)
        If HoloRACK.sPort = 0 Then startServer()
    End Sub
    Private Sub startServer()
        '// Dimension the variables
        Dim sqlPort As Integer
        Dim sqlHost, sqlDB, sqlUser, sqlPassword, sqlPassword_Hidden As String
        Dim dbCountCheck(2) As String

        With HoloRACK
            '// Set the system characters Chr(0-255) so we don't have to call the function everytime
            For P = 0 To 255
                sysChar(P) = Chr(P)
            Next

            Console.WriteLine("[SERVER] Starting up server for " & Environment.UserName & "...")
            Sleep(1000)
            Console.WriteLine("[SERVER] Attempting to retrieve settings from config.ini...")
            Console.WriteLine(vbNullString)
            Sleep(350)

            '// Set the directory where config.ini should be in the settings rack
            .configFileLocation = My.Application.Info.DirectoryPath & "\bin\config.ini"

            If My.Computer.FileSystem.FileExists(.configFileLocation) = False Then '// If the config.file in the /bin/ folder is not found
                '// Shutdown the server because the config.ini was not found
                Console.WriteLine("[SERVER] config.ini not found! Shutting down...")
                Sleep(1000)
                stopServer()
            End If

            Console.WriteLine("[SERVER] config.ini found at " & .configFileLocation)
            Console.WriteLine(vbNullString)

            '// Read the SQL details from the config.ini with the ReadINI function
            sqlHost = readINI("mysql", "host", .configFileLocation)
            sqlPort = Convert.ToInt16(readINI("mysql", "port", .configFileLocation))
            sqlUser = readINI("mysql", "username", .configFileLocation)
            sqlPassword = readINI("mysql", "password", .configFileLocation)
            sqlDB = readINI("mysql", "database", .configFileLocation)

            '// Make a line of ****s as long as the password, so you can run the server and others don't see the password
            sqlPassword_Hidden = vbNullString
            For P = 1 To sqlPassword.Length
                sqlPassword_Hidden += "*"
            Next

            If sqlPassword_Hidden = vbNullString Then sqlPassword_Hidden = "EMPTY" '// If the password is blank, then show 'EMPTY' as password

            '// Display we're attempting to connect, if it fails (so it returns false) then quit here
            Console.WriteLine("[MYSQL] Attempting to connect " & sqlDB & " on " & sqlHost & ":" & sqlPort & ", with UID: " & sqlUser & " and PWD: " & sqlPassword_Hidden)
            If HoloDB.openConnection(sqlHost, sqlPort, sqlDB, sqlUser, sqlPassword) = False Then Return

            Console.WriteLine("[MYSQL] Connection successfull.")
            Console.WriteLine(vbNullString)

            '// Read users, guestrooms and furnitures count from database
            dbCountCheck(0) = HoloDB.runRead("SELECT COUNT(*) FROM users")
            dbCountCheck(1) = HoloDB.runRead("SELECT COUNT(*) FROM guestrooms")
            dbCountCheck(2) = HoloDB.runRead("SELECT COUNT(*) FROM furniture")

            If dbCountCheck(0) = "" Then '// If there were no fields like that in the system table = something wrong with holodb
                Console.WriteLine("[MySQL] There is something wrong with database! Shutting down...")
                Sleep(400)
                stopServer()
            End If

            '// Load the settings from the config.ini file
            mainHoloAPP.loadPreferences()
            Console.WriteLine("[SERVER] Preferences succesfully loaded.")
            Console.WriteLine(vbNullString)

            '// Load the static room data (guestrooms)
            mainHoloAPP.loadRoomModels()
            Console.WriteLine("[SERVER] Loaded static guestroom data into memory.")
            Console.WriteLine(vbNullString)

            Sleep(400)

            Dim itemTemplateCount As Integer = HoloDB.runRead("SELECT COUNT(*) FROM catalogue_items LIMIT 1")

            cacheCatalogue()

            '// Perform some housekeeping
            HoloDB.runQuery("UPDATE guestrooms SET incnt_now = '0'")
            HoloDB.runQuery("UPDATE publicrooms SET incnt_now = '0'")

            Console.WriteLine("[MYSQL] Room inside counts reset.")
            HoloDB.runQuery("TRUNCATE TABLE sso")
            Console.WriteLine("[MYSQL] 'sso' table cleared.")
            Console.WriteLine(vbNullString)

            '// Display the current database counts
            Console.WriteLine("[MYSQL] Found " & dbCountCheck(0) & " users, " & dbCountCheck(1) & " guestrooms and " & dbCountCheck(2) & " furnitures.")
            Console.WriteLine(vbNullString)

            '// Read the port from the config.ini file, where the main socket has to listen on
            .sPort = readINI("sckmgr", "port", .configFileLocation)
            .maxConnections = readINI("sckmgr", "maxconnections", .configFileLocation)

            '// Set up the socket listener thread and start it
            Console.WriteLine("[SCKMGR] Setting up socket manager on port " & .sPort & "...")

            HoloSCKMGR.setupListener()

            serverMonitor.IsBackground = True
            serverMonitor.Priority = ThreadPriority.Lowest
            serverMonitor.Start()
        End With
    End Sub
    Friend Sub stopServer()
        Dim folCataPath As String
        Console.WriteLine(vbNullString)

        '// Close the database connection
        Console.WriteLine("[MYSQL] Closing existing database connection...")
        HoloDB.closeConnection()

        '// Dump the catalogue .bin files
        Console.WriteLine("[SERVER] Dumping catalogue .bin files...")

        Console.WriteLine("[SERVER] Shutting down...")

        '// Set the catalogue folder location
        folCataPath = My.Application.Info.DirectoryPath & "\bin\catalogue"

        '// Dump the folder (if it exists)
        'If My.Computer.FileSystem.DirectoryExists(folCataPath) = True Then My.Computer.FileSystem.DeleteDirectory(folCataPath, FileIO.DeleteDirectoryOption.DeleteAllContents)

        '// Stop the extra thread(s) if they are started
        If serverMonitor.IsAlive = True Then serverMonitor.Abort()

        '// Wait 1,5 seconds so the user can read the messages displayed
        Sleep(1500)

        '// Terminate the process
        Process.GetCurrentProcess.Kill()
    End Sub
    Private Sub checkCommand(ByVal inpCommand As String)
        Dim checkHeader As String

        If inpCommand.Contains(" ") Then checkHeader = inpCommand.Split(" ")(0) Else checkHeader = inpCommand
        Select Case checkHeader

            Case "about" '// Show information about Holograph Emulator
                Call Console.WriteLine("[ABOUT] Holograph is the light-weight open source VB Habbo Hotel emulator, check progress on RaGEZONE MMORPG Development forums.")

            Case "clear" '// Clear the console and re-print the starting message
                Console.Clear()
                printHoloProps()

            Case "exit", "shutdown" '// Shutdown the server
                stopServer()

            Case "stats" '// View the stats of your database
                If HoloRACK.sPort > 0 Then
                    Dim dbStatField() As String
                    dbStatField = HoloDB.runReadArray("SELECT users,guestrooms,furnitures FROM system")
                    Console.WriteLine("[STATS] Holograph Emulator found " & dbStatField(0) & " users, " & dbStatField(1) & " guestrooms and " & dbStatField(2) & " furnitures.")
                    Console.WriteLine("[STATS] Online users: " & HoloRACK.onlineCount & ", online peak: " & HoloRACK.onlinePeak & ", accepted connections: " & HoloRACK.acceptedConnections & ".")
                Else
                    Console.WriteLine("[ERROR] Not connected to database.")
                End If

            Case "show" '// Sub for showing some status
                Try
                    Dim showParameter As String = inpCommand.Split(" ")(1)
                    Select Case showParameter

                        Case "users"
                            Console.WriteLine(vbNullString)
                            Console.WriteLine("***********************************************************")
                            Console.WriteLine("Total online users count: " & HoloMANAGERS.hookedUsers.Count)
                            Console.WriteLine(vbNullString)
                            Console.WriteLine("ID" & vbTab & "NAME" & vbTab & "RANK" & vbTab & "USERID" & vbTab & "IPADDRESS")
                            Console.WriteLine("***********************************************************")
                            For Each hotelUser As clsHoloUSER In HoloMANAGERS.hookedUsers.Values
                                Console.WriteLine(hotelUser.classID & vbTab & hotelUser.userDetails.Name & vbTab & hotelUser.userDetails.Rank & vbTab & hotelUser.UserID & vbTab & hotelUser.holoSocket.RemoteEndPoint.ToString.Split(":")(0))
                            Next
                            Console.WriteLine("***********************************************************")
                            Console.WriteLine(vbNullString)
                        Case Else
                            Console.WriteLine("[COMMAND] Parameter '" & showParameter & "' is not found.")

                    End Select
                Catch ex As Exception
                    Console.WriteLine("[COMMAND] Supply a second parameter like 'users', cunt.")
                End Try

            Case "hotelalert"
                Dim hotelMessage As String = inpCommand.Split(" ")(1)
                For Each hotelUser As clsHoloUSER In HoloMANAGERS.hookedUsers.Values
                    hotelUser.transData("BK" & "Holograph Emulator says:\r" & hotelMessage & sysChar(1))
                Next

            Case Else '// Command not found
                Console.WriteLine("[COMMAND] Command '" & checkHeader & "' not found.")

        End Select
    End Sub
    Private Sub loadPreferences()
        '// Load the info for the rank profiles from the rank table in the database
        With HoloRACK

            For R = 1 To 7
                'rRank = HoloDB.runReadArray("SELECT ignorefilter,receivecfh,enterallrooms,seeallowners,admincatalogue,stafffloor,rightseverywhere FROM ranks WHERE rankid = '" & R & "'")
                HoloRANK(R) = New clsHoloRANK
                With HoloRANK(R)
                    .fuseRights = HoloDB.runReadArray("SELECT fuseright FROM rank_fuserights WHERE minrank <= '" & R & "'", True)
                    For f = 0 To .fuseRights.Count - 1
                        .strFuse += .fuseRights(f) & sysChar(2) '// Add this fuseright to the string for fuserights for this rank(all separated by CHAR2)
                    Next
                End With
            Next

            '// Load the wordfilter words
            HoloRACK.wordFilter_Words = HoloDB.runReadArray("SELECT word FROM wordfilter", True)
            HoloRACK.wordFilter_Replacement = HoloDB.runRead("SELECT wordfilter_replacement FROM system")

            '// Reset all preferences
            .wordFilter_Enabled = False
            .ssoLogin = False
            .welcMessage = vbNullString
            .chat_animations = True

            If readINI("game", "wordfilter", .configFileLocation) = "1" Then
                If HoloRACK.wordFilter_Words.Count = 0 Or HoloRACK.wordFilter_Replacement = vbNullString Then
                    Console.WriteLine("[WFILTER] Word filter was preferred as enabled but no words and/or replacement found, wordfilter disabled.")
                Else
                    HoloRACK.wordFilter_Enabled = True
                    Console.WriteLine("[WFILTER] Word filter enabled, " & HoloRACK.wordFilter_Words.Count & " word(s) found, replacement: " & HoloRACK.wordFilter_Replacement)
                End If
            Else
                Console.WriteLine("[WFILTER] Word filter disabled.")
            End If

            '// Load the login choice
            If readINI("login", "sso", .configFileLocation) = "1" Then .ssoLogin = True

            '// Load the welcome message from system table, if welcome messages are enabled
            If readINI("login", "welcome_message", .configFileLocation) = "1" Then .welcMessage = HoloDB.runRead("SELECT welcome_message FROM system")

            '// Load the 'Use animations during chat' choice
            If readINI("game", "chat_animations", .configFileLocation) = "1" Then .Chat_Animations = True

        End With
    End Sub
    Private Sub loadRoomModels()
        Dim m As Integer
        HoloRACK.roomModels = New Hashtable
        For m = 1 To 18
            HoloRACK.roomModels.Add(m, sysChar(m + 96))
            HoloSTATICMODEL(m) = New clsHoloSTATICMODEL

            Dim roomDoor() As String = HoloDB.runRead("SELECT door FROM guestroom_modeldata WHERE model = '" & HoloRACK.roomModels(m) & "' LIMIT 1").Split(",")
            With HoloSTATICMODEL(m)
                .doorX = roomDoor(0)
                .doorY = roomDoor(1)
                .doorH = Double.Parse(roomDoor(2))
                .strMap = HoloDB.runRead("SELECT map_height FROM guestroom_modeldata WHERE model = '" & HoloRACK.roomModels(m) & "' LIMIT 1")
            End With
        Next m
    End Sub
    Public Sub cacheCatalogue()
        Console.WriteLine(vbNullString)
        Console.WriteLine("[HOLOCACHE] Starting caching of catalogue + items, this may take a while...")

        Dim pageCount, itemCount As Integer
        Dim pageNames() As String = HoloDB.runReadArray("SELECT indexname FROM catalogue_pages ORDER BY indexid", True)

        HoloRACK.cataloguePages = New Hashtable
        For i = 0 To pageNames.Count - 1
            Dim pageData() As String = HoloDB.runReadArray("SELECT indexid,minrank,displayname,style_layout,img_header,img_side,label_description,label_misc,label_moredetails FROM catalogue_pages WHERE indexname = '" & pageNames(i) & "' LIMIT 1")
            If pageData.Count = 0 Then Continue For

            Dim curPageCache As New clsHoloRACK.cachedCataloguePage '// Create new instance of cached page
            curPageCache.displayName = pageData(2) '// Set display name for this page

            Dim pageBuilder As New System.Text.StringBuilder
            pageBuilder.Append("i:" & pageNames(i) & sysChar(13) & "n:" & pageData(2) & sysChar(13) & "l:" & pageData(3) & sysChar(13)) '// Add the required fields for catalogue page (indexname, showname, page layout style (boxes etc))
            If Not (pageData(4)) = vbNullString Then pageBuilder.Append("g:" & pageData(4) & sysChar(13)) '// If there's a headline image set, add it
            If Not (pageData(5)) = vbNullString Then pageBuilder.Append("e:" & pageData(5) & sysChar(13)) '// If there is/are side image(s) set, add it/them
            If Not (pageData(6)) = vbNullString Then pageBuilder.Append("h:" & pageData(6) & sysChar(13)) '// If there's a description set, add it
            If Not (pageData(8)) = vbNullString Then pageBuilder.Append("w:" & pageData(8) & sysChar(13)) '// If there's a 'Click here for more details' label set, add it
            If Not (pageData(7)) = vbNullString Then '// If the misc additions field is not blank
                Dim miscDetail() As String = pageData(7).Split(vbCrLf) '// Split the misc additions field to string array
                For m = 0 To miscDetail.Count - 1 : pageBuilder.Append(miscDetail(m) & sysChar(13)) : Next '// Go along all misc additions and add them, followed by Char13
            End If

            Dim pageItems() As String = HoloDB.runReadArray("SELECT catalogue_name,catalogue_description,catalogue_cost,tid,typeid,name_cct,length,width,colour,top FROM catalogue_items WHERE catalogue_id_page = '" & pageData(0) & "' ORDER BY catalogue_id_index ASC", True) '// Get the item data of all the items on this page IN ONE STRING (sorted ascending by the catalogue_id_index field) and split them to a string array
            For c = 0 To pageItems.Count - 1
                Dim itemData() As String = pageItems(c).Split(sysChar(9)) '// Split the current item string to a string array
                Dim templateID As Integer = itemData(3) '// Get the template ID of the current item

                HoloITEM(templateID) = New cachedItemTemplate(itemData(5), itemData(4), itemData(8), itemData(6), itemData(7), itemData(9)) '// Cache the item's details

                pageBuilder.Append("p:" & itemData(0) & sysChar(9) & itemData(1) & sysChar(9) & itemData(2) & sysChar(9) & sysChar(9)) '// Add the common fields for both wallitem/flooritem
                If HoloITEM(templateID).typeID = 0 Then pageBuilder.Append("i") Else pageBuilder.Append("s") '// Wallitem or flooritem? This will do the trick!!111
                pageBuilder.Append(sysChar(9) & itemData(5) & sysChar(9)) '// Add a char9 + the cctname + char9
                If HoloITEM(templateID).typeID = 0 Then pageBuilder.Append(sysChar(9)) Else pageBuilder.Append("0" & sysChar(9)) '// If wallitem, then just add a char9, if flooritem, then add a 0 + char9
                If HoloITEM(templateID).typeID = 0 Then pageBuilder.Append(sysChar(9)) Else pageBuilder.Append(HoloITEM(templateID).Length & "," & HoloITEM(templateID).Width & sysChar(9)) '// If wallitem, then just add a char9, if flooritem, then add the item's width, item's length and a char9
                pageBuilder.Append(itemData(5) & sysChar(9)) '// Add the cctname again + char9
                If HoloITEM(templateID).typeID > 0 Then pageBuilder.Append(HoloITEM(templateID).Colour) '// If it's a flooritem, then add the colour
                pageBuilder.Append(sysChar(13)) '// Add char13 to mark the end of the current item string
                itemCount += 1
            Next

            curPageCache.strPage = pageBuilder.ToString() '// Unfold the stringbuilder for the current page and stow it in the caching instance of this page his 'strPage' property
            HoloRACK.cataloguePages.Add(pageNames(i), curPageCache) '// Add the current page cache instance to the hashtable
            pageCount += 1
        Next '// Do the next page
        Console.WriteLine("[HOLOCACHE] Successfully cached " & pageCount & " catalogue pages and " & itemCount & " item templates!")
        Console.WriteLine(vbNullString)
    End Sub
    Private Sub monitorServer()
        While True
            Dim onlineCount As Integer = HoloMANAGERS.hookedUsers.Count
            Dim memUsage As Integer = GC.GetTotalMemory(False) / 1024
            Console.Title = "Holograph Emulator | online users: " & onlineCount & " | loaded rooms: " & HoloMANAGERS.hookedRooms.Count & " | RAM usage: " & memUsage & "KB"
            HoloDB.runQuery("UPDATE system SET onlinecount = '" & onlineCount & "'")
            Thread.Sleep(3500) '// Wait 3,5 seconds before updating stats again
        End While
    End Sub
End Module