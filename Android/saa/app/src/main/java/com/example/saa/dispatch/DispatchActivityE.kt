package com.example.saa.dispatch

import android.os.Bundle
import android.widget.ArrayAdapter
import android.widget.Button
import android.widget.EditText
import android.widget.ImageButton
import android.widget.Spinner
import android.widget.Toast
import androidx.activity.result.ActivityResultLauncher
import androidx.appcompat.app.AlertDialog
import androidx.appcompat.app.AppCompatActivity
import com.example.saa.DatabaseHelper
import com.example.saa.R
import com.example.saa.StationInfo
import com.journeyapps.barcodescanner.CaptureActivity
import com.journeyapps.barcodescanner.ScanOptions
import com.journeyapps.barcodescanner.ScanContract
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch

class DispatchActivityE : AppCompatActivity() {

    private lateinit var txtStart: EditText
    private lateinit var txtRackId: EditText
    private lateinit var txtWorkOrder:EditText
    private lateinit var scanStart: ImageButton
    private lateinit var scanWorkOrder : ImageButton
    private lateinit var btnSend: Button
    private lateinit var btnBack: Button
    private lateinit var btnRefresh:ImageButton
    private lateinit var spnPort: Spinner
    private val dbHelper = DatabaseHelper()
    private val buttonIds = listOf(
        R.id.buttonStationE5 to "E5",
        R.id.buttonStationE4 to "E4",
        R.id.buttonStationE3 to "E3",
        R.id.buttonStationE2 to "E2",
        R.id.buttonStationE1 to "E1"
    )
    private val startLauncher = registerForActivityResult(ScanContract()) { result ->
        handleScanResult(result.contents, txtStart)
    }


    private val workOrderLauncher = registerForActivityResult(ScanContract()) { result ->
        handleScanResult(result.contents, txtWorkOrder)
    }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContentView(R.layout.activity_dispatch_e)

        txtStart = findViewById(R.id.txtStart)
        txtRackId = findViewById(R.id.txtRackId)
        txtWorkOrder=findViewById(R.id.txtWorkOrder)
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

        for ((buttonId, station) in buttonIds) {
            findViewById<ImageButton>(buttonId).setOnClickListener {
                txtStart.setText(station)
                CoroutineScope(Dispatchers.Main).launch {
                    val stationList = dbHelper.getStationList()
                    val selectedStation = stationList.find { it.stationNo == station }
                    selectedStation?.let {
                        txtRackId.setText(it.rackID)
                    }
                }
            }
        }

        loadSpinnerData()
        setupStationButtons()
    }

    private fun handleScanResult(contents: String?, editText: EditText) {
        if (contents == null) {
            editText.setText("Cancel scan")
        } else {
            editText.setText(contents)
        }
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
        val start = txtStart.text.toString()
        val selectedPortInfo = spnPort.selectedItem as StationInfo
        val end = selectedPortInfo.stationNo
        val workOrder = txtWorkOrder.text.toString()
        val rackId = txtRackId.text.toString()

        if (start.isBlank() or workOrder.isBlank() or rackId.isBlank()) {
            Toast.makeText(this, "All fields must not be null", Toast.LENGTH_SHORT).show()
            return
        }

        val message = "Please confirm that the information is correct\nStartStation: $start\nEndStation: $end\nWorkOrder: $workOrder\nRackId: $rackId\n\nIf correct,please send。"

        AlertDialog.Builder(this)
            .setTitle("Check information")
            .setMessage(message)
            .setPositiveButton("Send") { dialog, _ ->
                dialog.dismiss()
                sendData(start, end, rackId,workOrder)
                viewRefresh()
            }
            .setNegativeButton("Cancle") { dialog, _ ->
                dialog.dismiss()
            }
            .show()
    }

    private fun sendData(start: String, end: String,rackId:String,wordOrder:String) {
        CoroutineScope(Dispatchers.Main).launch {
            var success = dbHelper.send_oNeed(start, end,rackId,wordOrder)
            if (success) {
                Toast.makeText(this@DispatchActivityE, "Date(oNeed) send success", Toast.LENGTH_SHORT).show()
            } else {
                Toast.makeText(this@DispatchActivityE, "Date(oNeed) send fail", Toast.LENGTH_SHORT).show()
            }
            success = dbHelper.change_oPort(end,rackId,"1",wordOrder)
            if (success) {
                Toast.makeText(this@DispatchActivityE, "Data(oPort_End) send success", Toast.LENGTH_SHORT).show()
            } else {
                Toast.makeText(this@DispatchActivityE, "Data(oPort_End) send fail", Toast.LENGTH_SHORT).show()
            }
        }
    }

    private fun loadSpinnerData() {
        CoroutineScope(Dispatchers.Main).launch {
            val allPorts = dbHelper.getStationList()
            // 过滤出符合条件的站点
            val filteredPorts = allPorts.filter {
                (it.stationNo.startsWith("A")||it.stationNo.startsWith("B")||it.stationNo.startsWith("D"))&&it.useFlag == "Y" && it.bgnToEnd == null && it.haveFlag == "0"
            }

            if (filteredPorts.isEmpty()) {
                Toast.makeText(this@DispatchActivityE, "Failed to retrieve valid station data from the database.", Toast.LENGTH_SHORT).show()
            } else {
                val adapter = ArrayAdapter(this@DispatchActivityE, android.R.layout.simple_spinner_item, filteredPorts)
                adapter.setDropDownViewResource(android.R.layout.simple_spinner_dropdown_item)
                spnPort.adapter = adapter
                Toast.makeText(this@DispatchActivityE, "Successfully loaded ${filteredPorts.size} station records.", Toast.LENGTH_SHORT).show()
            }
        }
    }


    private fun setupStationButtons() {
        CoroutineScope(Dispatchers.Main).launch {
            val stations = dbHelper.getStationList()

            for ((buttonId, stationNo) in buttonIds) {
                val stationInfo = stations.find { it.stationNo == stationNo }
                val button = findViewById<ImageButton>(buttonId)

                if (stationInfo != null && stationInfo.useFlag == "Y" && stationInfo.haveFlag == "1" && stationInfo.bgnToEnd == null) {
                    // 启用按钮并设置启用状态图像
                    button.isEnabled = true
                    button.alpha = 1f
                    button.setImageResource(R.drawable.agv_available) // 使用启用状态的图像
                } else if (stationInfo !=null && stationInfo.haveFlag=="0") {
                    // 禁用按钮并设置禁用状态图像
                    button.isEnabled = false
                    button.alpha=0.5f
                    button.setImageResource(R.drawable.agv) // 使用禁用状态的图像
                }else{
                    button.isEnabled=false
                    button.alpha=0.5f
                    button.setImageResource(R.drawable.agv_disable)
                }
            }
        }
    }

    private fun viewRefresh(){
        txtStart.text.clear()
        txtRackId.text.clear()
        txtWorkOrder.text.clear()
        setupStationButtons()
        loadSpinnerData()
    }
}
