package com.example.saa.dispatch

import android.os.Bundle
import android.text.Editable
import android.text.TextWatcher
import android.view.KeyEvent
import android.view.View
import android.widget.AdapterView
import android.widget.Button
import android.widget.EditText
import android.widget.ImageButton
import android.widget.Spinner
import android.widget.Toast
import androidx.activity.result.ActivityResultLauncher
import androidx.appcompat.app.AlertDialog
import androidx.appcompat.app.AppCompatActivity
import androidx.compose.ui.platform.findViewTreeCompositionContext
import com.example.saa.DatabaseHelper
import com.example.saa.R
import com.example.saa.SpinnerItem
import com.example.saa.SpinnerItemAdapter
import com.example.saa.oUserModel
import com.journeyapps.barcodescanner.CaptureActivity
import com.journeyapps.barcodescanner.ScanOptions
import com.journeyapps.barcodescanner.ScanContract
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext

class DispatchActivityNewC : AppCompatActivity() {

    private var user: oUserModel? = null
    private lateinit var spnArea: Spinner
    private lateinit var txtStart: EditText
    private lateinit var txtRackId: EditText
    private lateinit var txtWorkOrder:EditText
    private lateinit var txtBatchNo:EditText
    private lateinit var txtPartNo:EditText
    private lateinit var scanStart: ImageButton
    private lateinit var scanWorkOrder : ImageButton
    private lateinit var btnSend: Button
    private lateinit var btnBack: Button
    private lateinit var btnRefresh:ImageButton
    private lateinit var spnPort: Spinner
    private val dbHelper = DatabaseHelper()

    private val startLauncher = registerForActivityResult(ScanContract()) { result ->
        handleScanResult(result.contents, txtStart)
    }

    private val workOrderLauncher = registerForActivityResult(ScanContract()) { result ->
        handleScanResult(result.contents, txtWorkOrder)
    }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContentView(R.layout.activity_dispatch_new_c)

        user = intent.getParcelableExtra<oUserModel>("UserModel")
        spnArea = findViewById(R.id.spnArea)
        txtStart = findViewById(R.id.txtStart)
        txtRackId = findViewById(R.id.txtRackId)
        txtWorkOrder = findViewById(R.id.txtWorkOrder)
        txtBatchNo = findViewById(R.id.txtBatchNo)
        txtPartNo = findViewById(R.id.txtPartNo)
        scanStart = findViewById(R.id.scanStart)
        scanWorkOrder = findViewById(R.id.scanWorkOrder)
        btnSend = findViewById(R.id.btnSend)
        btnRefresh = findViewById(R.id.btnRefresh)
        btnBack = findViewById(R.id.btnBack)
        spnPort = findViewById(R.id.spnPort)

        btnSend.setOnClickListener {
            showConfirmationDialog()
        }

        btnRefresh.setOnClickListener{
            viewRefresh()
        }

        scanStart.setOnClickListener {
            launchBarcodeScanner(startLauncher)
        }

        scanWorkOrder.setOnClickListener{
            launchBarcodeScanner(workOrderLauncher)
        }

        btnBack.setOnClickListener {
            finish()
        }

        txtWorkOrder.addTextChangedListener(object : TextWatcher {
            override fun beforeTextChanged(s: CharSequence?, start: Int, count: Int, after: Int) {
                // This method is called to notify you that the text is about to be changed
            }

            override fun onTextChanged(s: CharSequence?, start: Int, before: Int, count: Int) {
                // This method is called to notify you that the text is being changed
                // Do something with the text here
                val currentText = s.toString()
                // For example, update a UI component with the new text
            }

            override fun afterTextChanged(s: Editable?) {
                // This method is called to notify you that the text has been changed
                spiltWorkOrder(s.toString())
            }
        })

        // Set the item selected listener
        spnArea.onItemSelectedListener = object : AdapterView.OnItemSelectedListener {
            override fun onItemSelected(parent: AdapterView<*>, view: View?, position: Int, id: Long) {
                /*// Get the selected item
                val selectedItem = parent.getItemAtPosition(position) as SpinnerItem
                // Handle the selected item
                handleSelectedItem(selectedItem)*/
                viewRefresh()

                val station = parent.getItemAtPosition(position) as SpinnerItem
                scanWorkOrder.isEnabled = station.value != "C"
            }

            override fun onNothingSelected(parent: AdapterView<*>) {
                // Handle the case where no item is selected if needed
            }
        }

        initialArea()

        // 設置鍵盤事件監聽器
        txtStart.setOnKeyListener { view, keyCode, event ->
            OnKeyPress(view, keyCode, event)
        }

        // 設置鍵盤事件監聽器
        txtWorkOrder.setOnKeyListener { view, keyCode, event ->
            OnKeyPress(view, keyCode, event)
        }

        // 點擊時選擇所有文本
        txtStart.setOnClickListener {
            OnClickListener(txtStart)
            //editText.selectAll()
        }

        // 點擊時選擇所有文本
        txtWorkOrder.setOnClickListener {
            OnClickListener(txtWorkOrder)
        }
    }

    private fun OnClickListener(editText: EditText) {
        try {
            editText.selectAll()
        } catch (e: Exception) {
            e.printStackTrace()
        }
    }

    // 處理鍵盤按下的事件
    private fun OnKeyPress(view: View, keyCode: Int, event: KeyEvent): Boolean {
        // 檢查按鍵事件是否是 "Enter" 並且是按下的事件
        if (event.action == KeyEvent.ACTION_DOWN && keyCode == KeyEvent.KEYCODE_ENTER) {
            // 將 view 轉型為 EditText 以取得文本數據
            val editText = view as EditText
            val scannedData = editText.text.toString()

            // 處理掃描數據
            handleScanResult(scannedData, editText)

            // 返回 true 表示已處理該事件
            return true
        }
        // 返回 false 表示未處理該事件，繼續傳遞
        return false
    }

    private fun initialArea() {
        try {
            val keyValuePairs = listOf(
                SpinnerItem("待料上料區", "C"),
                SpinnerItem("待料下料區", "D")
            )

            // Create and set the adapter
            val adapter = SpinnerItemAdapter(this, keyValuePairs)
            adapter.setDropDownViewResource(android.R.layout.simple_spinner_dropdown_item)
            spnArea.adapter = adapter

            // Set the initial selection (e.g., "待料上料區")
            spnArea.setSelection(0)
        } catch (e: Exception) {
            // Handle exceptions appropriately
            Toast.makeText(this, "Error selecting item", Toast.LENGTH_SHORT).show()
        }
    }

    private fun handleScanResult(contents: String?, editText: EditText) {
        if (contents == null) {
            //editText.setText("Cancel scan")
            Toast.makeText(this, "Cancel scan", Toast.LENGTH_SHORT).show()
            return
        }
        var scanData = contents
        // Use a single coroutine scope, ideally tied to the lifecycle of the activity or view model
        CoroutineScope(Dispatchers.Main).launch {
            try {
                // Fetch data from database in the IO dispatcher
                val oport = withContext(Dispatchers.IO) { dbHelper.getbyInterface(scanData) }

                if (editText == txtStart) {
                    var machineName: String? = ""
                    var bArea = checkArea(oport.StationNo)
                    if (!bArea) {
                        bArea = checkArea(scanData)
                        if (!bArea) {
                            viewRefresh()
                            Toast.makeText(this@DispatchActivityNewC, "Scan Wrong Area", Toast.LENGTH_SHORT).show()
                            return@launch
                        }
                    } else {
                        scanData = oport.StationNo
                    }

                    val stationList = withContext(Dispatchers.IO) { dbHelper.getAlloPort() }
                    val selectedStation = stationList.find { it.StationNo == scanData }
                    selectedStation?.let {
                        machineName = it.MachineName
                        if (scanData!!.substring(0, 1) == "D")
                            txtRackId.setText(it.RackID)
                    }

                    if (machineName.isNullOrEmpty())
                    {
                        Toast.makeText(this@DispatchActivityNewC, "Rack Mission Exist", Toast.LENGTH_SHORT).show()
                        return@launch
                    }

                    loadSpinnerData(scanData!!.substring(0, 1))
                    editText.contentDescription = scanData
                    editText.setText(machineName)
                } else if (editText == txtWorkOrder) {
                    editText.setText(oport.WorkOrder)
                }
            } catch (e: Exception) {
                // Handle exceptions appropriately
                Toast.makeText(this@DispatchActivityNewC, "Error processing scan result", Toast.LENGTH_SHORT).show()
            }
        }
    }

    private fun checkArea(scandata: String?): Boolean {
        var result = false
        try {
            var area = (spnArea.selectedItem as SpinnerItem).value
            val areasub = scandata?.substring(0, 1)
            if (areasub == area)
                result = true
        }
        catch (e: Exception) {
            e.printStackTrace()
        }

        return result
    }

    private fun launchBarcodeScanner(launcher: ActivityResultLauncher<ScanOptions>) {
        val options = createScanOptions()
        launcher.launch(options)
    }

    private fun createScanOptions(): ScanOptions {
        return ScanOptions().apply {
            setPrompt("Please align the QR Code.")
            setBeepEnabled(true)
            setOrientationLocked(false)
            captureActivity = CaptureActivity::class.java
        }
    }

    private fun showConfirmationDialog() {
        try {
            val end: String
            val start: String
            val workOrder: String
            val rackId: String

            if (txtStart.contentDescription.toString().substring(0, 1) == "C") {
                end = txtStart.contentDescription.toString()
                start = (spnPort.selectedItem as SpinnerItem).value
            }
            else
            {
                start = txtStart.contentDescription.toString()
                end = (spnPort.selectedItem as SpinnerItem).value
            }

            workOrder = txtWorkOrder.text.toString()
            rackId = txtRackId.text.toString()

            if (start.isBlank() or workOrder.isBlank() or rackId.isBlank()) {
                Toast.makeText(this, "All fields must not be null", Toast.LENGTH_SHORT).show()
                return
            }

            val message =
                "請確認資料正確性 (Please confirm data is correct)\n\n起點 (Start Station) : $start\n終點 (End Station) : $end\n工單 (Work Order) : $workOrder\n載盤 (Rack ID) : $rackId\n\n資料正確請按確認 (If correct,please send)"

            AlertDialog.Builder(this)
                .setTitle("資料檢查 (Check data)")
                .setMessage(message)
                .setPositiveButton("送出 (Send)") { dialog, _ ->
                    dialog.dismiss()
                    // Launch a coroutine to check the port and handle the result
                    CoroutineScope(Dispatchers.Main).launch {
                        val portResult = checkoNeed(start, end)
                        if (!portResult) {
                            sendData(start, end, rackId, workOrder)
                        } else {
                            Toast.makeText(this@DispatchActivityNewC, "資料重覆 (Data duplicated)", Toast.LENGTH_SHORT).show()
                        }
                    }
                }
                .setNegativeButton("取消 (Cancle)") { dialog, _ ->
                    dialog.dismiss()
                }
                .show()
        } catch (e: Exception) {
        }
    }

    private fun sendData(start: String, end: String,rackId:String,wordOrder:String) {
        CoroutineScope(Dispatchers.Main).launch {
            val assignFlag = if (start.substring(0, 1) == "B") "W" else null
            var success = dbHelper.send_oNeed(start, end, rackId, wordOrder, assignFlag)
            if (success) {
                Toast.makeText(this@DispatchActivityNewC, "Date(oNeed) send success", Toast.LENGTH_SHORT).show()
            } else {
                Toast.makeText(this@DispatchActivityNewC, "Date(oNeed) send fail", Toast.LENGTH_SHORT).show()
            }
            /*success = dbHelper.change_oPort(end, rackId, "3", "")
            if (success) {
                Toast.makeText(this@DispatchActivityNewC, "Data(oPort_End) send success", Toast.LENGTH_SHORT).show()
            } else {
                Toast.makeText(this@DispatchActivityNewC, "Data(oPort_End) send fail", Toast.LENGTH_SHORT).show()
            }*/

            viewRefresh()
        }
    }

    private suspend fun checkoNeed(start: String, end: String): Boolean {
        return withContext(Dispatchers.IO) {
            try {
                dbHelper.checkoNeed(start, end)
            } catch (e: Exception) {
                e.printStackTrace()
                false
            }
        }
    }

    private fun loadSpinnerData(block: String) {
        try {
            CoroutineScope(Dispatchers.Main).launch {
                spnPort.adapter = null
                val oportList = dbHelper.getAlloPort()
                // 过滤出符合条件的站点
                val filteredPorts = when (block) {
                    "C" -> oportList.filter {
                        it.Block == "B" && it.UseFlag == "Y" && it.BgnToEnd.isNullOrEmpty() && it.HaveFlag == "3"
                    }
                    "D" -> oportList.filter {
                        it.Block == "E" && it.UseFlag == "Y" && it.BgnToEnd.isNullOrEmpty() && it.HaveFlag == "0"
                    }
                    else -> emptyList()
                }

                if (filteredPorts.isNullOrEmpty()) {
                    Toast.makeText(this@DispatchActivityNewC, "Failed to retrieve valid station data from the database.", Toast.LENGTH_SHORT).show()
                } else {
                    // Create SpinnerItem list from filteredPorts
                    val items = filteredPorts.map { SpinnerItem(it.StationNo + getKey(it.WorkOrder!!), it.StationNo) }
                    // Create and set custom adapter
                    val adapter = SpinnerItemAdapter(this@DispatchActivityNewC, items)

                    //val adapter = ArrayAdapter(this@DispatchActivityNewC, android.R.layout.simple_spinner_item, filteredPorts.map { it.StationNo })
                    adapter.setDropDownViewResource(android.R.layout.simple_spinner_dropdown_item)
                    spnPort.adapter = adapter

                    //val adapter = ArrayAdapter(this@DispatchActivityNewC, android.R.layout.simple_spinner_item, filteredPorts.map { it.StationNo })
                    //adapter.setDropDownViewResource(android.R.layout.simple_spinner_dropdown_item)
                    //spnPort.adapter = adapter
                    if (block == "C") {
                        spinnerItemSelectChange()
                        spnPort.isEnabled = true
                    }
                    else
                        spnPort.isEnabled = false

                    //Toast.makeText(this@DispatchActivityC, "Successfully loaded ${filteredPorts.size} station records.", Toast.LENGTH_SHORT).show()
                }
            }
        } catch (e: Exception) {
        }
    }

    fun getKey(workorder: String): String
    {
        val temp = spiltWorkOrder(workorder)
        if (temp.size < 2)
            return ""

        // Default values in case the conditions are not met
        val batchNo = if (temp.size > 2) temp[2] else ""
        val partNo = if (temp.size > 3) temp[3] else ""

        return "-$partNo-$batchNo"
    }

    fun spiltWorkOrder(workorder: String): Array<String> {
        val order = workorder.split("^").toTypedArray()
        setOrderText(order)
        return order
    }

    fun setOrderText(scanOrder: Array<String>)
    {
        val batchNo = if (scanOrder.size > 2) scanOrder[2] else ""
        val partNo = if (scanOrder.size > 3) scanOrder[3] else ""
        txtBatchNo.setText(batchNo)
        txtPartNo.setText(partNo)
    }

    private fun spinnerItemSelectChange() {
        // 設置 ItemSelected 監聽器
        spnPort.onItemSelectedListener = object : AdapterView.OnItemSelectedListener {
            override fun onItemSelected(parent: AdapterView<*>?, view: View?, position: Int, id: Long) {
                // 獲取選中的項目
                val station = parent?.getItemAtPosition(position) as SpinnerItem

                // 顯示選中的項目
                CoroutineScope(Dispatchers.Main).launch {
                    val oportList = dbHelper.getAlloPort()
                    val selectedStation = oportList.find { it.StationNo == station.value }
                    selectedStation?.let {
                        txtWorkOrder.setText(it.WorkOrder)
                        if (station.value.substring(0, 1) == "B")
                            txtRackId.setText(it.RackID)
                    }
                }

                // 顯示選中的項目
                //Toast.makeText(this@DispatchActivityC, "Selected: $station", Toast.LENGTH_SHORT).show()
            }

            override fun onNothingSelected(parent: AdapterView<*>?)
            {
                // 沒有選擇任何項目時的處理
                txtWorkOrder.text.clear()
                txtRackId.text.clear()
                // 沒有選擇任何項目時的處理
                //Toast.makeText(this@DispatchActivityC, "Nothing selected", Toast.LENGTH_SHORT).show()
            }
        }
    }

    private fun viewRefresh() {
        txtStart.text.clear()
        txtStart.contentDescription = ""
        txtRackId.text.clear()
        txtWorkOrder.text.clear()
        txtBatchNo.text.clear()
        txtPartNo.text.clear()
        spnPort.adapter = null
        scanWorkOrder.isEnabled = true
    }
}