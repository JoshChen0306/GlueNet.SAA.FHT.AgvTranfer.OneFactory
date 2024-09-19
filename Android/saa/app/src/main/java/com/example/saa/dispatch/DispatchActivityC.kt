package com.example.saa.dispatch

import android.os.Bundle
import android.text.Editable
import android.text.TextWatcher
import android.view.View
import android.widget.AdapterView
import android.widget.ArrayAdapter
import android.widget.Button
import android.widget.EditText
import android.widget.ImageButton
import android.widget.Spinner
import android.widget.Toast
import androidx.activity.result.ActivityResultLauncher
import androidx.appcompat.app.AlertDialog
import androidx.appcompat.app.AppCompatActivity
import androidx.lifecycle.lifecycleScope
import com.example.saa.DatabaseHelper
import com.example.saa.R
import com.example.saa.oUserModel
import com.journeyapps.barcodescanner.CaptureActivity
import com.journeyapps.barcodescanner.ScanOptions
import com.journeyapps.barcodescanner.ScanContract
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext

class DispatchActivityC : AppCompatActivity() {

    private var user: oUserModel? = null
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
        setContentView(R.layout.activity_dispatch_c)

        user = intent.getParcelableExtra<oUserModel>("UserModel")
        txtStart = findViewById(R.id.txtStart)
        txtRackId = findViewById(R.id.txtRackId)
        txtWorkOrder=findViewById(R.id.txtWorkOrder)
        txtBatchNo=findViewById(R.id.txtBatchNo)
        txtPartNo=findViewById(R.id.txtPartNo)
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

        loadSpinnerData()

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
    }

    private fun handleScanResult(contents: String?, editText: EditText) {
        if (contents == null) {
            //editText.setText("Cancel scan")
            Toast.makeText(this, "Cancel scan", Toast.LENGTH_SHORT).show()
        } else {
            if (editText == txtStart) {
                val bArea = checkArea(contents)
                if (!bArea) {
                    viewRefresh()
                    Toast.makeText(this, "Scan Wrong Area", Toast.LENGTH_SHORT).show()
                    return
                }

                CoroutineScope(Dispatchers.Main).launch {
                    val oportList = dbHelper.getAlloPort()
                    val selectedStation = oportList.find { it.StationNo == contents }
                    selectedStation?.let {
                        txtWorkOrder.setText(it.WorkOrder)
                        txtRackId.setText(it.RackID)
                    }
                }
            }

            editText.setText(contents)
        }
    }

    private fun checkArea(scandata: String): Boolean {
        var result = false
        try {
            val areasub = scandata.substring(0, 1)
            val area = (64 + user?.groupId!!.toInt()).toChar().toString()
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
            val end = txtStart.text.toString()
            val start = spnPort.selectedItem.toString()
            val workOrder = txtWorkOrder.text.toString()
            val rackId = txtRackId.text.toString()

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
                            Toast.makeText(this@DispatchActivityC, "資料重覆 (Data duplicated)", Toast.LENGTH_SHORT).show()
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
            var success = dbHelper.send_oNeed(start, end, rackId, wordOrder, "W")
            if (success) {
                Toast.makeText(this@DispatchActivityC, "Date(oNeed) send success", Toast.LENGTH_SHORT).show()
            } else {
                Toast.makeText(this@DispatchActivityC, "Date(oNeed) send fail", Toast.LENGTH_SHORT).show()
            }
            /*success = dbHelper.change_oPort(end, rackId, "1", wordOrder)
            if (success) {
                Toast.makeText(this@DispatchActivityC, "Data(oPort_End) send success", Toast.LENGTH_SHORT).show()
            } else {
                Toast.makeText(this@DispatchActivityC, "Data(oPort_End) send fail", Toast.LENGTH_SHORT).show()
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

    private fun loadSpinnerData() {
        try {
            CoroutineScope(Dispatchers.Main).launch {
                val oportList = dbHelper.getAlloPort()
                // 过滤出符合条件的站点
                val filteredPorts = oportList.filter {
                    it.StationNo.startsWith("B") && it.UseFlag == "Y" && it.BgnToEnd.isNullOrEmpty() && it.HaveFlag == "3"
                }

                if (filteredPorts.isNullOrEmpty()) {
                    Toast.makeText(this@DispatchActivityC, "Failed to retrieve valid station data from the database.", Toast.LENGTH_SHORT).show()
                } else {
                    val adapter = ArrayAdapter(this@DispatchActivityC, android.R.layout.simple_spinner_item, filteredPorts.map { it.StationNo })
                    adapter.setDropDownViewResource(android.R.layout.simple_spinner_dropdown_item)
                    spnPort.adapter = adapter
                    spinnerItemSelectChange()
                    //Toast.makeText(this@DispatchActivityC, "Successfully loaded ${filteredPorts.size} station records.", Toast.LENGTH_SHORT).show()
                }
            }
        }
        catch (e: Exception)
        {
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
                val station = parent?.getItemAtPosition(position)

                // 顯示選中的項目
                CoroutineScope(Dispatchers.Main).launch {
                    val oportList = dbHelper.getAlloPort()
                    val selectedStation = oportList.find { it.StationNo == station }
                    selectedStation?.let {
                        txtWorkOrder.setText(it.WorkOrder)
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
        txtRackId.text.clear()
        txtWorkOrder.text.clear()
        txtBatchNo.text.clear()
        txtPartNo.text.clear()
        loadSpinnerData()
    }
}