Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.Globalization
Imports System.Linq
Imports System.Windows.Forms

Public Class RowEditForm
    Inherits Form

    Private ReadOnly inputControls As New Dictionary(Of String, Control)(StringComparer.OrdinalIgnoreCase)
    Private ReadOnly columnList As List(Of String)
    Private ReadOnly valuesInternal As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
    Private ReadOnly dirtyFields As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
    Private ReadOnly activeUser As String
    Private ReadOnly activeRole As String
    Private ReadOnly canFullEdit As Boolean
    Private ReadOnly canApprovalEdit As Boolean

    Public ReadOnly Property Values As Dictionary(Of String, String)
        Get
            Return valuesInternal
        End Get
    End Property

    Public Sub New(columns As IEnumerable(Of String), initialValues As Dictionary(Of String, String), windowTitle As String, currentUser As String, userRole As String, fullEditAllowed As Boolean, approvalEditAllowed As Boolean)
        Me.activeUser = currentUser
        Me.activeRole = If(userRole, "")
        Me.canFullEdit = fullEditAllowed
        Me.canApprovalEdit = approvalEditAllowed
        Me.columnList = columns.ToList()

        For Each col In columnList
            If initialValues IsNot Nothing AndAlso initialValues.ContainsKey(col) Then
                valuesInternal(col) = initialValues(col)
            Else
                valuesInternal(col) = ""
            End If
        Next

        Me.Text = windowTitle
        Me.StartPosition = FormStartPosition.CenterParent
        Me.Size = New Size(1160, 700)
        Me.MinimumSize = New Size(980, 610)
        Me.Font = New Font("Segoe UI", 9.0F)
        Me.BackColor = Color.FromArgb(243, 246, 250)

        AppIconHelper.ApplyIcon(Me)
        Me.AutoScroll = False
        BuildUi()
    End Sub

    Private Sub BuildUi()
        Dim root As New TableLayoutPanel()
        root.Dock = DockStyle.Fill
        root.ColumnCount = 1
        root.RowCount = 3
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 44))
        root.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 64))
        Me.Controls.Add(root)

        Dim header As New Panel()
        header.Dock = DockStyle.Fill
        header.BackColor = Color.FromArgb(31, 78, 121)
        header.Padding = New Padding(16, 5, 16, 4)
        root.Controls.Add(header, 0, 0)

        Dim lblTitle As New Label()
        lblTitle.Text = "Satır Bilgileri Düzenle"
        lblTitle.ForeColor = Color.White
        lblTitle.Font = New Font("Segoe UI", 13.5F, FontStyle.Bold)
        lblTitle.Dock = DockStyle.Fill
        lblTitle.TextAlign = ContentAlignment.MiddleLeft
        header.Controls.Add(lblTitle)

        Dim bodyPanel As New Panel()
        bodyPanel.Dock = DockStyle.Fill
        bodyPanel.AutoScroll = False
        bodyPanel.Padding = New Padding(10, 8, 10, 6)
        root.Controls.Add(bodyPanel, 0, 1)

        Dim mainStack As New TableLayoutPanel()
        mainStack.Dock = DockStyle.Fill
        mainStack.ColumnCount = 1
        mainStack.RowCount = 2
        mainStack.RowStyles.Add(New RowStyle(SizeType.Percent, 74))
        mainStack.RowStyles.Add(New RowStyle(SizeType.Percent, 26))
        mainStack.Margin = New Padding(0)
        bodyPanel.Controls.Add(mainStack)

        Dim twoCol As New TableLayoutPanel()
        twoCol.Dock = DockStyle.Fill
        twoCol.ColumnCount = 2
        twoCol.RowCount = 1
        twoCol.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50))
        twoCol.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50))
        twoCol.Margin = New Padding(0)
        mainStack.Controls.Add(twoCol, 0, 0)

        Dim leftStack As New TableLayoutPanel()
        leftStack.Dock = DockStyle.Fill
        leftStack.ColumnCount = 1
        leftStack.RowCount = 2
        leftStack.RowStyles.Add(New RowStyle(SizeType.Percent, 45))
        leftStack.RowStyles.Add(New RowStyle(SizeType.Percent, 55))
        leftStack.Margin = New Padding(0, 0, 7, 0)

        Dim rightStack As New TableLayoutPanel()
        rightStack.Dock = DockStyle.Fill
        rightStack.ColumnCount = 1
        rightStack.RowCount = 1
        rightStack.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
        rightStack.Margin = New Padding(7, 0, 0, 0)

        twoCol.Controls.Add(leftStack, 0, 0)
        twoCol.Controls.Add(rightStack, 1, 0)

        Dim fullStack As New TableLayoutPanel()
        fullStack.Dock = DockStyle.Fill
        fullStack.ColumnCount = 1
        fullStack.RowCount = 1
        fullStack.Margin = New Padding(0, 8, 0, 0)
        mainStack.Controls.Add(fullStack, 0, 1)

        Dim basicCols = OrderExisting({"SIRA NO", "SIRA", "INO TALEP TARİHİ", "INO TALEP TARIHI", "SAYAÇ ADI", "SİPARİŞ YERİ", "İŞ EMRİ NO"})
        Dim ino1Cols = columnList.Where(Function(c) NormalizeCol(c).StartsWith("INO1")).ToList()
        Dim ino2Cols = columnList.Where(Function(c) NormalizeCol(c).StartsWith("INO2")).ToList()
        Dim measureCols = OrderExisting({"Q4", "Q3", "ARA DEBİ", "ARA DEBI", "Q2", "Q1", "TAM (+)", "TAM (-)"})
        Dim noteCols = columnList.Where(Function(c) NormalizeCol(c).Contains("ACIKLAMA")).ToList()

        Dim used As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        AddUsed(used, basicCols)
        AddUsed(used, ino1Cols)
        AddUsed(used, ino2Cols)
        AddUsed(used, measureCols)
        AddUsed(used, noteCols)

        Dim otherCols = columnList.Where(Function(c) Not used.Contains(c)).ToList()

        Dim combinedIno2Cols As New List(Of String)()
        combinedIno2Cols.AddRange(ino2Cols)
        combinedIno2Cols.AddRange(measureCols)
        combinedIno2Cols.AddRange(otherCols)

        If basicCols.Count > 0 Then AddSection(leftStack, "Temel Bilgiler", basicCols, 2, 0, 0)
        If ino1Cols.Count > 0 Then AddSection(leftStack, "İNO-1 Bilgileri", ino1Cols, 2, 0, 1)
        If combinedIno2Cols.Count > 0 Then AddSection(rightStack, "İNO-2 Bilgileri", combinedIno2Cols, 2, 0, 0)
        If noteCols.Count > 0 Then AddSection(fullStack, "Açıklama", noteCols, 1, 0, 0)

        Dim footer As New TableLayoutPanel()
        footer.Dock = DockStyle.Fill
        footer.BackColor = Color.White
        footer.Padding = New Padding(10, 10, 14, 10)
        footer.ColumnCount = 3
        footer.RowCount = 1
        footer.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        footer.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 132.0F))
        footer.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 124.0F))
        footer.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        root.Controls.Add(footer, 0, 2)

        Dim readOnlyView = String.IsNullOrWhiteSpace(activeUser)

        Dim btnCancel As New Button()
        btnCancel.Text = If(readOnlyView, "Kapat", "Vazgeç")
        btnCancel.Size = New Size(118, 38)
        btnCancel.BackColor = Color.FromArgb(238, 242, 247)
        btnCancel.ForeColor = Color.FromArgb(52, 64, 84)
        btnCancel.FlatStyle = FlatStyle.Flat
        btnCancel.FlatAppearance.BorderSize = 0
        btnCancel.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        btnCancel.Margin = New Padding(0)
        btnCancel.Anchor = AnchorStyles.None
        btnCancel.AutoEllipsis = False
        btnCancel.DialogResult = DialogResult.Cancel

        Dim btnSave As New Button()
        btnSave.Text = "Kaydet"
        btnSave.Size = New Size(118, 38)
        btnSave.BackColor = Color.FromArgb(15, 123, 63)
        btnSave.ForeColor = Color.White
        btnSave.FlatStyle = FlatStyle.Flat
        btnSave.FlatAppearance.BorderSize = 0
        btnSave.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        btnSave.Margin = New Padding(0)
        btnSave.Anchor = AnchorStyles.None
        btnSave.AutoEllipsis = False

        AddHandler btnSave.Click, AddressOf BtnSave_Click

        If Not readOnlyView Then
            footer.Controls.Add(btnCancel, 1, 0)
            footer.Controls.Add(btnSave, 2, 0)
            Me.AcceptButton = btnSave
        Else
            footer.Controls.Add(btnCancel, 2, 0)
            Me.AcceptButton = btnCancel
        End If

        Me.CancelButton = btnCancel
    End Sub

    Private Function OrderExisting(preferred As IEnumerable(Of String)) As List(Of String)
        Dim result As New List(Of String)()

        For Each p In preferred
            For Each actual In columnList
                If String.Equals(NormalizeCol(actual), NormalizeCol(p), StringComparison.OrdinalIgnoreCase) Then
                    If Not result.Any(Function(x) String.Equals(x, actual, StringComparison.OrdinalIgnoreCase)) Then
                        result.Add(actual)
                    End If
                End If
            Next
        Next

        Return result
    End Function

    Private Sub AddUsed(target As HashSet(Of String), cols As IEnumerable(Of String))
        For Each c In cols
            target.Add(c)
        Next
    End Sub

    Private Function NormalizeCol(v As String) As String
        If v Is Nothing Then Return ""
        Dim t = v.Trim().ToUpperInvariant()
        t = t.Replace("İ", "I").Replace("İ", "I").Replace("Ş", "S").Replace("Ğ", "G").Replace("Ü", "U").Replace("Ö", "O").Replace("Ç", "C").Replace("ı", "I")
        t = t.Replace("-", "").Replace(" ", "")
        Return t
    End Function

    Private Function IsSiraColumn(columnName As String) As Boolean
        Dim n = NormalizeCol(columnName)
        Return n = "SIRA" OrElse n = "SIRANO"
    End Function

    Private Function IsInoTalepTarihiColumn(columnName As String) As Boolean
        Dim n = NormalizeCol(columnName)
        Return n = "INOTALEPTARIHI" OrElse n = "INOTALEPTARIH"
    End Function

    Private Function IsIno1VerilenBolumColumn(columnName As String) As Boolean
        Dim n = NormalizeCol(columnName)
        Return n = "INO1VERILENBOLUM" OrElse n = "INO1VERILENBOLÜM" OrElse n = "INO1VERILENBÖLÜM"
    End Function

    Private Function IsIno1OnayVerenColumn(columnName As String) As Boolean
        Dim n = NormalizeCol(columnName)
        Return n = "INO1ONAYVEREN" OrElse n = "INO1ONAYIVEREN"
    End Function

    Private Function IsIno2OnayVerenColumn(columnName As String) As Boolean
        Dim n = NormalizeCol(columnName)
        Return n = "INO2ONAYVEREN" OrElse n = "INO2ONAYIVEREN"
    End Function

    Private Function IsInoDurumuColumn(columnName As String) As Boolean
        Dim n = NormalizeCol(columnName)
        Return n = "INO1DURUM" OrElse n = "INO1DURUMU" OrElse n = "INO2DURUM" OrElse n = "INO2DURUMU"
    End Function

    Private Function IsIno2DurumuColumn(columnName As String) As Boolean
        Dim n = NormalizeCol(columnName)
        Return n = "INO2DURUM" OrElse n = "INO2DURUMU"
    End Function

    Private Function IsIno1DurumuColumn(columnName As String) As Boolean
        Dim n = NormalizeCol(columnName)
        Return n = "INO1DURUM" OrElse n = "INO1DURUMU"
    End Function

    Private Function IsMeasurementColumn(columnName As String) As Boolean
        Dim n = NormalizeCol(columnName)

        Return n = "Q4" OrElse
               n = "Q3" OrElse
               n = "Q2" OrElse
               n = "Q1" OrElse
               n.Contains("ARADEBI") OrElse
               n.StartsWith("TAM")
    End Function

    Private Function IsReportNoColumn(columnName As String) As Boolean
        Dim n = NormalizeCol(columnName)
        Return n = "INO1RAPORNO" OrElse n = "INO2RAPORNO"
    End Function

    Private Function IsDateColumn(columnName As String) As Boolean
        Dim n = NormalizeCol(columnName)
        Return n.Contains("TARIH")
    End Function

    Private Function IsAdminRole() As Boolean
        Return String.Equals(activeRole, UserStore.RoleAdmin, StringComparison.OrdinalIgnoreCase) OrElse
               String.Equals(activeUser, "ADMİN", StringComparison.OrdinalIgnoreCase)
    End Function

    Private Function IsMechanismRole() As Boolean
        Return String.Equals(activeRole, UserStore.RoleMechanism, StringComparison.OrdinalIgnoreCase) OrElse
               String.Equals(activeUser, "MEKANİZMA", StringComparison.OrdinalIgnoreCase)
    End Function

    Private Function IsApprovalUserRole() As Boolean
        Return String.Equals(activeRole, UserStore.RoleApproval, StringComparison.OrdinalIgnoreCase) OrElse
               String.Equals(activeUser, "OZAN ÇAĞLAYAN", StringComparison.OrdinalIgnoreCase) OrElse
               String.Equals(activeUser, "GÜLİZ KARTAL", StringComparison.OrdinalIgnoreCase) OrElse
               String.Equals(activeUser, "NESLİHAN ŞENOL", StringComparison.OrdinalIgnoreCase) OrElse
               String.Equals(activeUser, "AYAR", StringComparison.OrdinalIgnoreCase)
    End Function

    Private Function IsExplanationColumn(columnName As String) As Boolean
        Return NormalizeCol(columnName).Contains("ACIKLAMA")
    End Function

    Private Function IsBasicInfoColumn(columnName As String) As Boolean
        Dim n = NormalizeCol(columnName)

        Return n = "SIRA" OrElse
               n = "SIRANO" OrElse
               n = "INOTALEPTARIHI" OrElse
               n = "INOTALEPTARIH" OrElse
               n = "SAYACADI" OrElse
               n = "SIPARISYERI" OrElse
               n = "ISEMRINO"
    End Function

    Private Function IsInoInfoColumn(columnName As String) As Boolean
        Dim n = NormalizeCol(columnName)

        If IsMeasurementColumn(columnName) Then Return True

        Return n.StartsWith("INO1") OrElse n.StartsWith("INO2")
    End Function

    Private Function IsFieldEditable(columnName As String) As Boolean
        ' ADMİN tüm alanları değiştirebilir.
        If IsAdminRole() Then Return True

        ' Diğer rollerde kilitli kalacak sistem alanları.
        If IsSiraColumn(columnName) OrElse
           IsInoTalepTarihiColumn(columnName) OrElse
           IsReportNoColumn(columnName) OrElse
           IsIno2DurumuColumn(columnName) Then
            Return False
        End If

        ' MEKANİZMA yalnızca Temel Bilgiler ve Açıklama alanlarını değiştirebilir.
        If IsMechanismRole() Then
            Return IsBasicInfoColumn(columnName) OrElse IsExplanationColumn(columnName)
        End If

        ' Onay kullanıcıları yalnızca İNO-1, İNO-2 ve Açıklama bilgilerini değiştirebilir.
        If IsApprovalUserRole() Then
            Return IsInoInfoColumn(columnName) OrElse IsExplanationColumn(columnName)
        End If

        Return False
    End Function

    Private Sub SelectComboValuePreservingExisting(cmb As ComboBox, value As String)
        Dim existingValue = If(value, "").Trim()

        For i As Integer = 0 To cmb.Items.Count - 1
            If String.Equals(Convert.ToString(cmb.Items(i)), existingValue, StringComparison.OrdinalIgnoreCase) Then
                cmb.SelectedIndex = i
                Return
            End If
        Next

        ' Eski CSV kayıtlarında artık seçenek listesinde bulunmayan kullanıcı/durum
        ' değerleri olabilir. Düzenleme ekranı bu değeri boş seçeneğe çevirmemelidir.
        If existingValue.Length > 0 Then
            cmb.Items.Add(existingValue)
            cmb.SelectedIndex = cmb.Items.Count - 1
        ElseIf cmb.Items.Count > 0 Then
            cmb.SelectedIndex = 0
        End If
    End Sub

    Private Sub RegisterDirtyTracking(columnName As String, ctrl As Control)
        If ctrl Is Nothing OrElse Not IsFieldEditable(columnName) Then Return

        If TypeOf ctrl Is ComboBox Then
            Dim cmb = DirectCast(ctrl, ComboBox)
            AddHandler cmb.SelectedIndexChanged,
                Sub()
                    dirtyFields.Add(columnName)
                    If IsMeasurementColumn(columnName) Then UpdateIno2DurumuFromMeasurements()
                End Sub
        ElseIf TypeOf ctrl Is DateTimePicker Then
            Dim dtp = DirectCast(ctrl, DateTimePicker)
            Dim initialChecked = dtp.Checked
            Dim initialValue = dtp.Value.Date
            Dim markDateDirty As Action =
                Sub()
                    If dtp.Checked <> initialChecked OrElse
                       (dtp.Checked AndAlso dtp.Value.Date <> initialValue) Then
                        dirtyFields.Add(columnName)
                    End If
                End Sub

            AddHandler dtp.ValueChanged, Sub() markDateDirty()
            AddHandler dtp.MouseUp, Sub(sender As Object, e As MouseEventArgs) markDateDirty()
            AddHandler dtp.KeyUp, Sub(sender As Object, e As KeyEventArgs) markDateDirty()
        ElseIf TypeOf ctrl Is TextBox Then
            Dim txt = DirectCast(ctrl, TextBox)
            AddHandler txt.TextChanged, Sub() dirtyFields.Add(columnName)
        End If
    End Sub

    Private Sub ApplyReadOnlyStyle(ctrl As Control, editable As Boolean)
        If editable Then Return

        If TypeOf ctrl Is TextBox Then
            Dim txt = DirectCast(ctrl, TextBox)
            txt.ReadOnly = True
            txt.BackColor = Color.FromArgb(245, 247, 250)
            txt.ForeColor = Color.FromArgb(102, 112, 133)
            txt.TabStop = False
        ElseIf TypeOf ctrl Is ComboBox Then
            ctrl.Enabled = False
        ElseIf TypeOf ctrl Is DateTimePicker Then
            ctrl.Enabled = False
        End If
    End Sub

    Private Sub AddSection(parent As TableLayoutPanel, sectionTitle As String, cols As List(Of String), fieldsPerRow As Integer, colPos As Integer, rowPos As Integer)
        If cols Is Nothing OrElse cols.Count = 0 Then Return

        Dim borderColor As Color = Color.FromArgb(220, 226, 234)
        Dim innerBackColor As Color = Color.White
        Dim titleColor As Color = Color.FromArgb(31, 78, 121)

        If String.Equals(sectionTitle, "İNO-1 Bilgileri", StringComparison.OrdinalIgnoreCase) Then
            borderColor = Color.FromArgb(208, 219, 232)
            innerBackColor = Color.FromArgb(246, 249, 253)
            titleColor = Color.FromArgb(62, 95, 138)
        ElseIf String.Equals(sectionTitle, "İNO-2 Bilgileri", StringComparison.OrdinalIgnoreCase) Then
            borderColor = Color.FromArgb(226, 217, 210)
            innerBackColor = Color.FromArgb(252, 249, 247)
            titleColor = Color.FromArgb(123, 96, 83)
        End If

        Dim sectionCard As New Panel()
        sectionCard.Dock = DockStyle.Fill
        sectionCard.Margin = New Padding(0, 0, 0, 6)
        sectionCard.Padding = New Padding(1)
        sectionCard.BackColor = borderColor

        Dim inner As New Panel()
        inner.Dock = DockStyle.Fill
        inner.BackColor = innerBackColor
        inner.Padding = New Padding(9, 7, 9, 7)
        sectionCard.Controls.Add(inner)

        Dim title As New Label()
        title.Text = sectionTitle
        title.Dock = DockStyle.Bottom
        title.Height = 20
        title.Font = New Font("Segoe UI", 9.5F, FontStyle.Bold)
        title.ForeColor = titleColor
        inner.Controls.Add(title)

        Dim grid As New TableLayoutPanel()
        grid.Dock = DockStyle.Fill
        grid.ColumnCount = fieldsPerRow
        grid.RowCount = 0
        grid.Margin = New Padding(0)
        grid.Padding = New Padding(0)

        For i As Integer = 1 To fieldsPerRow
            grid.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, CSng(100.0 / fieldsPerRow)))
        Next

        Dim rowIndex As Integer = -1

        For i As Integer = 0 To cols.Count - 1
            Dim currentCol = cols(i)

            If i Mod fieldsPerRow = 0 Then
                grid.RowCount += 1
                grid.RowStyles.Add(New RowStyle(SizeType.Absolute, 46))
                rowIndex += 1
            End If

            Dim cellPanel = CreateFieldPanel(currentCol, valuesInternal(currentCol))
            cellPanel.Margin = New Padding(0, 0, 8, 3)

            If fieldsPerRow = 1 Then
                cellPanel.Margin = New Padding(0, 0, 0, 3)
            ElseIf (i Mod fieldsPerRow) = fieldsPerRow - 1 Then
                cellPanel.Margin = New Padding(0, 0, 0, 3)
            End If

            grid.Controls.Add(cellPanel, i Mod fieldsPerRow, rowIndex)
        Next

        inner.Controls.Add(grid)

        parent.Controls.Add(sectionCard, colPos, rowPos)
    End Sub

    Private Function CreateFieldPanel(columnName As String, value As String) As Panel
        Dim p As New Panel()
        p.Dock = DockStyle.Fill
        p.Margin = New Padding(0)

        Dim lbl As New Label()
        lbl.Text = columnName
        lbl.Dock = DockStyle.Top
        lbl.Height = 16
        lbl.Font = New Font("Segoe UI", 7.8F, FontStyle.Bold)
        lbl.ForeColor = Color.FromArgb(52, 64, 84)
        lbl.Margin = New Padding(0)

        Dim input As Control

        If IsInoTalepTarihiColumn(columnName) Then
            Dim txtTalep As New TextBox()
            txtTalep.Text = value
            txtTalep.Dock = DockStyle.Top
            txtTalep.BorderStyle = BorderStyle.FixedSingle
            txtTalep.Font = New Font("Segoe UI", 8.2F)
            txtTalep.Height = 23
            txtTalep.ReadOnly = True
            txtTalep.BackColor = Color.FromArgb(245, 247, 250)
            txtTalep.ForeColor = Color.FromArgb(102, 112, 133)
            txtTalep.TabStop = False
            input = txtTalep
        ElseIf IsReportNoColumn(columnName) Then
            Dim txtReport As New TextBox()
            txtReport.Text = value
            txtReport.Dock = DockStyle.Top
            txtReport.BorderStyle = BorderStyle.FixedSingle
            txtReport.Font = New Font("Segoe UI", 8.2F)
            txtReport.Height = 23
            txtReport.ReadOnly = True
            txtReport.BackColor = Color.FromArgb(245, 247, 250)
            txtReport.ForeColor = Color.FromArgb(102, 112, 133)
            txtReport.TabStop = False
            input = txtReport
        ElseIf IsIno1OnayVerenColumn(columnName) Then
            Dim cmbApprover1 As New ComboBox()
            cmbApprover1.Dock = DockStyle.Top
            cmbApprover1.Height = 23
            cmbApprover1.Font = New Font("Segoe UI", 8.2F)
            cmbApprover1.DropDownStyle = ComboBoxStyle.DropDownList
            cmbApprover1.Items.AddRange(New Object() {"", "OZAN", "GÜLİZ", "NESLİHAN"})
            SelectComboValuePreservingExisting(cmbApprover1, value)
            input = cmbApprover1
        ElseIf IsIno2OnayVerenColumn(columnName) Then
            Dim cmbApprover2 As New ComboBox()
            cmbApprover2.Dock = DockStyle.Top
            cmbApprover2.Height = 23
            cmbApprover2.Font = New Font("Segoe UI", 8.2F)
            cmbApprover2.DropDownStyle = ComboBoxStyle.DropDownList
            cmbApprover2.Items.AddRange(New Object() {"", "OZAN", "GÜLİZ", "NESLİHAN", "AYAR"})
            SelectComboValuePreservingExisting(cmbApprover2, value)
            input = cmbApprover2
        ElseIf IsIno1VerilenBolumColumn(columnName) Then
            Dim cmb As New ComboBox()
            cmb.Dock = DockStyle.Top
            cmb.Height = 23
            cmb.Font = New Font("Segoe UI", 8.2F)
            cmb.DropDownStyle = ComboBoxStyle.DropDownList
            cmb.Items.AddRange(New Object() {"", "MEKANİZMA", "PLASTİKHANE", "TALAŞLI"})
            SelectComboValuePreservingExisting(cmb, value)

            input = cmb
        ElseIf IsIno2DurumuColumn(columnName) Then
            Dim txtIno2Durum As New TextBox()
            txtIno2Durum.Text = value
            txtIno2Durum.Dock = DockStyle.Top
            txtIno2Durum.BorderStyle = BorderStyle.FixedSingle
            txtIno2Durum.Font = New Font("Segoe UI", 8.2F)
            txtIno2Durum.Height = 23
            txtIno2Durum.ReadOnly = True
            txtIno2Durum.BackColor = Color.FromArgb(245, 247, 250)
            txtIno2Durum.ForeColor = Color.FromArgb(102, 112, 133)
            txtIno2Durum.TabStop = False
            input = txtIno2Durum
        ElseIf IsIno1DurumuColumn(columnName) Then
            Dim cmbDurum As New ComboBox()
            cmbDurum.Dock = DockStyle.Top
            cmbDurum.Height = 23
            cmbDurum.Font = New Font("Segoe UI", 8.2F)
            cmbDurum.DropDownStyle = ComboBoxStyle.DropDownList
            cmbDurum.Items.AddRange(New Object() {"", "UYGUN", "UYGUN DEĞİL"})

            Dim existingDurum = If(value, "").Trim()

            If NormalizeCol(existingDurum).Contains("UYGUNDEGIL") OrElse NormalizeCol(existingDurum).Contains("RED") Then
                cmbDurum.SelectedIndex = 2
            ElseIf NormalizeCol(existingDurum).Contains("UYGUN") OrElse NormalizeCol(existingDurum).Contains("ONAY") Then
                cmbDurum.SelectedIndex = 1
            Else
                SelectComboValuePreservingExisting(cmbDurum, existingDurum)
            End If
            input = cmbDurum
        ElseIf IsMeasurementColumn(columnName) Then
            Dim cmbMeasure As New ComboBox()
            cmbMeasure.Dock = DockStyle.Top
            cmbMeasure.Height = 23
            cmbMeasure.Font = New Font("Segoe UI", 8.2F)
            cmbMeasure.DropDownStyle = ComboBoxStyle.DropDownList
            cmbMeasure.Items.AddRange(New Object() {"", "UYGUN", "UYGUN DEĞİL"})

            Dim existingMeasure = If(value, "").Trim()

            If NormalizeCol(existingMeasure).Contains("UYGUNDEGIL") OrElse NormalizeCol(existingMeasure).Contains("RED") Then
                cmbMeasure.SelectedIndex = 2
            ElseIf NormalizeCol(existingMeasure).Contains("UYGUN") OrElse NormalizeCol(existingMeasure).Contains("ONAY") Then
                cmbMeasure.SelectedIndex = 1
            Else
                SelectComboValuePreservingExisting(cmbMeasure, existingMeasure)
            End If
            input = cmbMeasure
        ElseIf IsDateColumn(columnName) Then
            Dim dtp As New DateTimePicker()
            dtp.Dock = DockStyle.Top
            dtp.Height = 23
            dtp.Format = DateTimePickerFormat.Custom
            dtp.CustomFormat = "dd.MM.yyyy"
            dtp.ShowCheckBox = True
            dtp.Font = New Font("Segoe UI", 8.2F)

            Dim parsed As DateTime
            If TryParseDate(value, parsed) Then
                dtp.Value = parsed
                dtp.Checked = True
            Else
                dtp.Value = DateTime.Today
                dtp.Checked = False
            End If

            input = dtp
        Else
            Dim txt As New TextBox()
            txt.Text = value
            txt.Dock = DockStyle.Top
            txt.BorderStyle = BorderStyle.FixedSingle
            txt.Font = New Font("Segoe UI", 8.2F)
            txt.Margin = New Padding(0)
            txt.BackColor = Color.White
            txt.ForeColor = Color.FromArgb(23, 32, 51)

            If NormalizeCol(columnName).Contains("ACIKLAMA") Then
                txt.Multiline = True
                txt.Height = 72
                txt.ScrollBars = ScrollBars.Vertical
            Else
                txt.Height = 23
            End If

            If IsSiraColumn(columnName) Then
                txt.ReadOnly = True
                txt.BackColor = Color.FromArgb(245, 247, 250)
                txt.ForeColor = Color.FromArgb(102, 112, 133)
                txt.TabStop = False
            End If

            input = txt
        End If

        inputControls(columnName) = input
        ApplyReadOnlyStyle(input, IsFieldEditable(columnName))
        RegisterDirtyTracking(columnName, input)

        p.Controls.Add(input)
        p.Controls.Add(lbl)

        Return p
    End Function

    Private Function GetSelectedControlValue(ctrl As Control) As String
        If ctrl Is Nothing Then Return ""

        If TypeOf ctrl Is DateTimePicker Then
            Dim dtp = DirectCast(ctrl, DateTimePicker)
            If dtp.Checked Then Return dtp.Value.ToString("dd.MM.yyyy")
            Return ""
        End If

        If TypeOf ctrl Is ComboBox Then
            Dim selected = DirectCast(ctrl, ComboBox).SelectedItem
            If selected Is Nothing Then Return ""
            Return Convert.ToString(selected).Trim()
        End If

        If TypeOf ctrl Is TextBox Then
            Return If(DirectCast(ctrl, TextBox).Text, "").Trim()
        End If

        Return ""
    End Function

    Private Function FindIno2DurumuColumn() As String
        For Each col In columnList
            If IsIno2DurumuColumn(col) Then Return col
        Next

        Return ""
    End Function

    Private Function CalculateIno2DurumuFromMeasurements() As String
        Dim measurementCols = columnList.Where(Function(c) IsMeasurementColumn(c)).ToList()

        If measurementCols.Count = 0 Then Return ""

        Dim allSuitable As Boolean = True

        For Each col In measurementCols
            Dim value As String = ""

            If inputControls.ContainsKey(col) Then
                value = GetSelectedControlValue(inputControls(col))
            ElseIf valuesInternal.ContainsKey(col) Then
                value = valuesInternal(col)
            End If

            Dim n = NormalizeCol(value)

            If n.Contains("UYGUNDEGIL") OrElse n.Contains("RED") Then
                Return "UYGUN DEĞİL"
            End If

            If Not n.Contains("UYGUN") Then
                allSuitable = False
            End If
        Next

        If allSuitable Then Return "UYGUN"

        Return ""
    End Function

    Private Sub UpdateIno2DurumuFromMeasurements()
        Dim ino2DurumCol = FindIno2DurumuColumn()

        If String.IsNullOrWhiteSpace(ino2DurumCol) Then Return

        Dim result = CalculateIno2DurumuFromMeasurements()
        valuesInternal(ino2DurumCol) = result

        If inputControls.ContainsKey(ino2DurumCol) Then
            inputControls(ino2DurumCol).Text = result
        End If
    End Sub

    Private Function TryParseDate(value As String, ByRef result As DateTime) As Boolean
        If String.IsNullOrWhiteSpace(value) Then Return False

        Dim formats = New String() {"dd.MM.yyyy", "d.M.yyyy", "dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd", "dd-MM-yyyy", "d-M-yyyy"}

        If DateTime.TryParseExact(value.Trim(), formats, CultureInfo.GetCultureInfo("tr-TR"), DateTimeStyles.None, result) Then
            Return True
        End If

        Return DateTime.TryParse(value.Trim(), CultureInfo.GetCultureInfo("tr-TR"), DateTimeStyles.None, result)
    End Function

    Private Sub BtnSave_Click(sender As Object, e As EventArgs)
        ' Yalnızca kullanıcının gerçekten değiştirdiği alanları geri yaz.
        ' Böylece kilitli alanlar ve eski CSV değerleri başka bir alan düzenlenirken korunur.
        For Each columnName In dirtyFields
            If inputControls.ContainsKey(columnName) AndAlso IsFieldEditable(columnName) Then
                valuesInternal(columnName) = GetSelectedControlValue(inputControls(columnName))
            End If
        Next

        If dirtyFields.Any(Function(columnName) IsMeasurementColumn(columnName)) Then
            Dim ino2DurumCol = FindIno2DurumuColumn()
            If Not String.IsNullOrWhiteSpace(ino2DurumCol) Then
                valuesInternal(ino2DurumCol) = CalculateIno2DurumuFromMeasurements()
            End If
        End If

        Me.DialogResult = DialogResult.OK
        Me.Close()
    End Sub
End Class
