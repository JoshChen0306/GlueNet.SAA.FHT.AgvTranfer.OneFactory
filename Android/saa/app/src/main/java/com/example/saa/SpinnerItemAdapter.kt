package com.example.saa

import android.content.Context
import android.view.LayoutInflater
import android.view.View
import android.view.ViewGroup
import android.widget.ArrayAdapter
import android.widget.TextView

/*class SpinnerItemAdapter(context: Context, items: List<SpinnerItem>) : ArrayAdapter<SpinnerItem>(context, 0, items) {

    override fun getView(position: Int, convertView: View?, parent: ViewGroup): View {
        return createViewFromResource(position, convertView, parent)
    }

    override fun getDropDownView(position: Int, convertView: View?, parent: ViewGroup): View {
        return createViewFromResource(position, convertView, parent)
    }

    private fun createViewFromResource(position: Int, convertView: View?, parent: ViewGroup): View {
        val view = convertView ?: LayoutInflater.from(context).inflate(androidx.appcompat.R.layout.support_simple_spinner_dropdown_item, parent, false)
        val item = getItem(position)
        val textView = view.findViewById<TextView>(android.R.id.text1)
        textView.text = item?.key
        return view
    }
}*/

class SpinnerItemAdapter(context: Context, private val items: List<SpinnerItem>)
    : ArrayAdapter<SpinnerItem>(context, androidx.appcompat.R.layout.support_simple_spinner_dropdown_item, items) {

    override fun getView(position: Int, convertView: View?, parent: ViewGroup): View {
        val view = convertView ?: LayoutInflater.from(context).inflate(androidx.appcompat.R.layout.support_simple_spinner_dropdown_item, parent, false)
        val textView = view.findViewById<TextView>(android.R.id.text1)
        textView.text = items[position].key // Set the text for the item
        return view
    }

    override fun getDropDownView(position: Int, convertView: View?, parent: ViewGroup): View {
        return getView(position, convertView, parent) // Use the same view for dropdown
    }
}