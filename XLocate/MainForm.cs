using System;
using System.Configuration;
using System.Collections.Generic;
using System.Collections;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Net;
using System.Windows.Forms;
using System.IO;

using System.Linq;
using XServer;

namespace XLocate
{
    public partial class MainForm : Form
    {
        CustomLayer customLayer = new CustomLayer();
        //XMapWSService svcMap = new XMapWSService();
        XLocateWSService locateService = new XLocateWSService();
        ImageInfo imageInfo = new ImageInfo();
        MapSection mapSection = new MapSection();
        ResultAddress[] resultAddresses = null;
        ResultCombinedTransport[] resultCombinedTransports = null;
        ResultField[] resultField_Address;
        ResultField[] resultField_Location;
        ResultField[] resultField_AddressByText;
        ResultField[] resultField_CombinedTransport;
        double taCount = 0, taCur = double.NaN, taTotal = 0.0;
        double taMax = double.NaN, taMin = double.NaN, taAvg = double.NaN;

        public MainForm()
        {
            if (File.Exists(@"d:\xservers source\private.txt"))
            {
                using (var reader = new StreamReader(@"d:\xservers source\private.txt"))
                {
                    Properties.Settings.Default.xmap_password = reader.ReadLine();
                    Properties.Settings.Default.xlocate_password = Properties.Settings.Default.xmap_password;
                }
            }

            //locateService.Proxy = new WebProxy("http://localhost.:8888");


            InitializeComponent();
            //
            fillCBO(cboASTERISKMODE, new string[] { "0 - disable asterisk mode", "1 - search at the beginning", "2 - search at the end", "3 - search at beginning and at the end" });
            cboASTERISKMODE.SelectedIndex = Properties.Settings.Default.ASTERISKMODE;            //
            //
            fillCBO(cboCOUNTRY_CODETYPE, new string[] { "0 - Country name", "1 - ISO2", "2 - ISO3", "3 - country code plate" });
            cboCOUNTRY_CODETYPE.SelectedIndex = Properties.Settings.Default.COUNTRY_CODETYPE;
            //
            fillCBO(cboRevCOUNTRY_CODETYPE, new string[] { "0 - Country name", "1 - ISO2", "2 - ISO3", "3 - country code plate" });
            cboRevCOUNTRY_CODETYPE.SelectedIndex = Properties.Settings.Default.RevCOUNTRY_CODETYPE;
            //
            fillCBO(cboSTREET_HNRPOSITION, new string[] { "0 - do not search for house numbers", "1 - search at the beginning", "2 - search at the end", "3 - search at beginning and at the end" });
            cboSTREET_HNRPOSITION.SelectedIndex = Properties.Settings.Default.STREET_HNRPOSITION;
            //
            fillCBO(cboSWAPANDSPLITMODE, new string[] { "1 - swap city and city2", "2 - city2 becomes city, city will be empty", "4 - city becomes city2, city2 will be empty", "8 - split city into city and city2" });
            cboSWAPANDSPLITMODE.SelectedIndex = Properties.Settings.Default.SWAPANDSPLITMODE;
            //
            fillCBO(cboCoordFormat, new string[] { "OG_GEODECIMAL", "PTV_MERCATOR", "PTV_GEOMINSEC", "PTV_GEODECIMAL", "PTV_CONFORM", "PTV_SUPERCONFORM", "PTV_SMARTUNITS" });
            cboCoordFormat.SelectedIndex = Properties.Settings.Default.CoordFormat;
            // 
            fillCBO(cboResponseGeometry, new string[] { "PLAIN - Plain coordinates", "WKB - Well Known Binary Format ", "WKT - Well Known Text Format", "KML - Keyhole Markup Language" });
            cboResponseGeometry.SelectedIndex = Properties.Settings.Default.ResponseGeometry;
            //
            customLayer.centerObjects = true;
            customLayer.drawPriority = Properties.Settings.Default.RESULTADDRESS_PRIORITY;
            customLayer.name = Properties.Settings.Default.RESULTADDRESSLAYER_NAME;
            customLayer.objectInfos = ObjectInfoType.REFERENCEPOINT;
            customLayer.visible = true;
            //
            tbxServiceAddressLocate.Text = Properties.Settings.Default.xlocate;
            tbxServiceAddressMap.Text = Properties.Settings.Default.xmap;
            //
            tbxCountry.Text = Properties.Settings.Default.COUNTRY;
            tbxState.Text = Properties.Settings.Default.STATE;
            tbxPostCode.Text = Properties.Settings.Default.POSTCODE;
            tbxCity.Text = Properties.Settings.Default.CITY;
            tbxCity2.Text = Properties.Settings.Default.CITY2;
            tbxStreet.Text = Properties.Settings.Default.STREET;
            tbxHouseNumber.Text = Properties.Settings.Default.HOUSENR;
            tbxLocationX.Text = Properties.Settings.Default.LOCATION_X.ToString();
            tbxLocationY.Text = Properties.Settings.Default.LOCATION_Y.ToString();

            tbxENGINE_SEARCHRANGE.Text = Properties.Settings.Default.ENGINE_SEARCHRANGE;
            tbxENGINE_SEARCHDETAILLEVEL.Text = Properties.Settings.Default.ENGINE_SEARCHDETAILLEVEL;

            // 20100408: new resultFields 1.10 invented
            cbxUseNewFields.Checked = Properties.Settings.Default.UseNewFields;
            if (Properties.Settings.Default.REQUEST_ADDFITIONALFIELDS)
            {
                createResultField_Address();
                createResultField_Location();
                createResultField_AddressByText();
                // 2011-12-21 Migration 1.14
                createResultField_CombinedTransport();
            }
            // 20100709 - MapProfile 
            tbxMapProfile.Text = Properties.Settings.Default.MapProfile;
            //2011-03-23 Single field search...
            singleFieldTextTxtBx.Text = Properties.Settings.Default.SingleFieldText;
            singleFieldCountryTxtBx.Text = Properties.Settings.Default.SingleFieldCountry;
            singleFieldSeparatorsTxtBx.Text = Properties.Settings.Default.SINGLE_FIELD_SEPARATORS;

            // Username / Password via Basic HTTP Authentification
            locateService.Credentials = new NetworkCredential(Properties.Settings.Default.xlocate_username, Properties.Settings.Default.xlocate_password);

            toolTip1.AutoPopDelay = int.MaxValue;

            resultSplitContainer.Panel2Collapsed = true;
        }

        private void btnProcessAddress_Click(object sender, EventArgs e)
        {
            Button evtButton = (Button)sender;

            // TODO check if reset buttons resets everything
            resetButtons();
            List<Layer> lstLayer = new List<Layer>();
            CallerContext cc = new CallerContext()
            {
                log1 = Properties.Settings.Default.CallerContext_Log1,
                log2 = Properties.Settings.Default.CallerContext_Log2,
                log3 = Properties.Settings.Default.CallerContext_Log3
            };
            string exMessage = "";

            try
            {
                evtButton.BackColor = System.Drawing.Color.Yellow;
                evtButton.Update();
                tbxRES_COUNT.Text = "";
                tbxErrorcode.Text = "";
                tbxTAcurr.Text = "";
                DateTime startTime;

                locateService.Url = tbxServiceAddressLocate.Text;

                if (cbxCallerContextProperties.Checked)
                {
                    CallerContextProperty ccpResponseGeometry = new CallerContextProperty()
                    {
                        key = "ResponseGeometry",
                        value = getSelectedString(cboResponseGeometry)
                    };
                    if (!ccpResponseGeometry.value.Contains("PLAIN"))
                        ccpResponseGeometry.value += ",PLAIN";

                    CallerContextProperty ccpCoordFormat = new CallerContextProperty()
                    {
                        key = "CoordFormat",
                        value = getSelectedString(cboCoordFormat)
                    };

                    CallerContextProperty ccpProfile = new CallerContextProperty()
                    {
                        key = "Profile",
                        value = tbxProfile.Text
                    };

                    cc.wrappedProperties = new CallerContextProperty[] { ccpCoordFormat, ccpProfile, ccpResponseGeometry };
                }

                resultAddresses = null;
                resultCombinedTransports = null;
                AddressResponse addressResponse = null;
                CombinedTransportResponse combinedTransportResponse = null;
                ObjectResponse objectResponse = null;

                if (evtButton.Equals(btnFindAddress))
                {
                    // Input address is generated by the text boxes values            
                    Address inputAddress = new Address()
                    {
                        country = tbxCountry.Text,
                        state = tbxState.Text,
                        postCode = tbxPostCode.Text,
                        city = tbxCity.Text,
                        city2 = tbxCity2.Text,
                        street = tbxStreet.Text,
                        houseNumber = tbxHouseNumber.Text
                    };

                    // SearchOption array is generated
                    XServer.SearchOptionBase[] searchOption = null;
                    List<XServer.SearchOptionBase> listSearchOptions = new List<XServer.SearchOptionBase>();
                    if (cbxSearchOptions.Checked)
                    {
                        listSearchOptions.Add(buildSearchOption(SearchParameter.SEARCH_BINARY, cbxSEARCH_BINARY.Checked ? "1" : "0"));
                        listSearchOptions.Add(buildSearchOption(SearchParameter.SEARCH_PHONETIC, cbxSEARCH_PHONETIC.Checked ? "1" : "0"));
                        listSearchOptions.Add(buildSearchOption(SearchParameter.SEARCH_FUZZY, cbxSEARCH_FUZZY.Checked ? "1" : "0"));
                        listSearchOptions.Add(buildSearchOption(SearchParameter.STREET_RETURNALLHNR, cbxSTREET_RETURNALLHNR.Checked ? "1" : "0"));
                        listSearchOptions.Add(buildSearchOption(SearchParameter.CITY_RETURNALLCITY2, cbxCITY_RETURNALLCITY2.Checked ? "1" : "0"));
                        listSearchOptions.Add(buildSearchOption(SearchParameter.MULTIWORDINDEX_ENABLE, cbxMULTIWORDINDEX_ENABLE.Checked ? "1" : "0"));
                        listSearchOptions.Add(buildSearchOption(SearchParameter.POSTCODE_AGGREGATE, cbxPOSTCODE_AGGREGATE.Checked ? "1" : "0"));
                        listSearchOptions.Add(buildSearchOption(SearchParameter.INTERSECTIONS_ENABLE, cbxINTERSECTIONS_ENABLE.Checked ? "1" : "0"));
                        listSearchOptions.Add(buildSearchOption(SearchParameter.ASTERISKMODE, cboASTERISKMODE));
                        listSearchOptions.Add(buildSearchOption(SearchParameter.COUNTRY_CODETYPE, cboCOUNTRY_CODETYPE));
                        listSearchOptions.Add(buildSearchOption(SearchParameter.STREET_HNRPOSITION, cboSTREET_HNRPOSITION));
                        listSearchOptions.Add(buildSearchOption(SearchParameter.SWAPANDSPLITMODE, cboSWAPANDSPLITMODE));
                        listSearchOptions.Add(buildSearchOption(SearchParameter.HNR_OFFSET, tbxHNR_OFFSET.Text));
                        listSearchOptions.Add(buildSearchOption(SearchParameter.RESULT_LANGUAGE, tbxRESULT_LANGUAGE.Text));
                        //
                        if (tbxMAX_RESULT.Text != "")
                            listSearchOptions.Add(buildSearchOption(SearchParameter.MAX_RESULT, tbxMAX_RESULT.Text));
                        //
                    }
                    searchOption = listSearchOptions.ToArray();

                    startTime = DateTime.Now;
                    addressResponse = locateService.findAddress(inputAddress, searchOption, null, resultField_Address, cc);
                    displayTransactionTime(startTime);

                }
                else if (evtButton.Equals(btnFindLocation))
                {
                    Location inputLocation = new Location();
                    inputLocation.coordinate = new XServer.Point();
                    inputLocation.coordinate.point = new PlainPoint()
                    {
                        x = Convert.ToDouble(tbxLocationX.Text),
                        y = Convert.ToDouble(tbxLocationY.Text)
                    };
                    XServer.Bitmap centerBitmap = new XServer.Bitmap()
                    {
                        descr = "center",
                        name = "location_rough.bmp",
                        position = inputLocation.coordinate
                    };

                    CustomLayer centerLayer = new CustomLayer()
                    {
                        centerObjects = true,
                        drawPriority = 1000,
                        name = "Center",
                        objectInfos = ObjectInfoType.GEOMETRY,
                        visible = true,
                        wrappedBitmaps = new Bitmaps[]{
                            new Bitmaps(){
                                wrappedBitmaps = new BasicBitmap[]{
                                    centerBitmap
                                }
                            }
                        }
                    };
                    lstLayer.Add(centerLayer);

                    //
                    List<ReverseSearchOption> lstReverseSearchOptions = new List<ReverseSearchOption>();
                    if (cbxReverseSearchOptions.Checked)
                    {
                        lstReverseSearchOptions.Add(buildReverseSearchOption(ReverseSearchParameter.COUNTRY_CODETYPE, cboRevCOUNTRY_CODETYPE));
                        if (tbxRevENGINE_TARGETSIZE.Text != "")
                            lstReverseSearchOptions.Add(buildReverseSearchOption(ReverseSearchParameter.ENGINE_TARGETSIZE, tbxRevENGINE_TARGETSIZE.Text));
                        if (tbxRevENGINE_TOLERANCE.Text != "")
                            lstReverseSearchOptions.Add(buildReverseSearchOption(ReverseSearchParameter.ENGINE_TOLERANCE, tbxRevENGINE_TOLERANCE.Text));
                        if (tbxRevRESULT_LANGUAGE.Text != "")
                            lstReverseSearchOptions.Add(buildReverseSearchOption(ReverseSearchParameter.RESULT_LANGUAGE, tbxRevRESULT_LANGUAGE.Text));
                        if (tbxENGINE_FILTERMODE.Text != "")
                            lstReverseSearchOptions.Add(buildReverseSearchOption(ReverseSearchParameter.ENGINE_FILTERMODE, tbxENGINE_FILTERMODE.Text));
                        if (tbxENGINE_SEARCHDETAILLEVEL.Text != "")
                            lstReverseSearchOptions.Add(buildReverseSearchOption(ReverseSearchParameter.ENGINE_SEARCHDETAILLEVEL, tbxENGINE_SEARCHDETAILLEVEL.Text));
                        if (tbxENGINE_SEARCHRANGE.Text != "")
                            lstReverseSearchOptions.Add(buildReverseSearchOption(ReverseSearchParameter.ENGINE_SEARCHRANGE, tbxENGINE_SEARCHRANGE.Text));
                    }

                    startTime = DateTime.Now;
                    //locateService.EnableDecompression = true;
                    addressResponse = locateService.findLocation(inputLocation, lstReverseSearchOptions.ToArray(), null, resultField_Location, cc);
                    displayTransactionTime(startTime);
                    if (cbxFilterCity.Checked && (addressResponse.wrappedResultList != null))
                        addressResponse.wrappedResultList = filter2city(addressResponse.wrappedResultList);

                }
                // 2011-03-23 SingleField Search
                else if (evtButton.Equals(btnFindAddressByText))
                {
                    string sfAddress = singleFieldTextTxtBx.Text;
                    string sfCountry = singleFieldCountryTxtBx.Text;
                    XServer.SearchOption[] arrSO;
                    if (singleFieldSeparatorsTxtBx.Text != "")
                    {
                        XServer.SearchOption soSINGLE_FIELD_SEPARATORS = new XServer.SearchOption()
                        {
                            param = SearchParameter.SINGLE_FIELD_SEPARATORS,
                            value = singleFieldSeparatorsTxtBx.Text
                        };
                        arrSO = new XServer.SearchOption[] { soSINGLE_FIELD_SEPARATORS };
                    }
                    else
                    {
                        arrSO = null;
                    }

                    startTime = DateTime.Now;
                    addressResponse = locateService.findAddressByText(sfAddress, sfCountry, arrSO, null, resultField_AddressByText, cc);
                    displayTransactionTime(startTime);
                }
                // 2011-12-21 Migration 1.14
                else if (evtButton.Equals(btnFindCombinedTransportByLocation))
                {
                    XServer.Location inputLocation = new XServer.Location()
                    {
                        coordinate = new XServer.Point()
                        {
                            point = new PlainPoint()
                            {
                                x = Convert.ToDouble(tbxLocationX.Text),
                                y = Convert.ToDouble(tbxLocationY.Text)
                            }
                        }
                    };

                    List<ReverseSearchOption> lstReverseSearchOptions = new List<ReverseSearchOption>();
                    if (cbxReverseSearchOptions.Checked)
                    {
                        lstReverseSearchOptions.Add(buildReverseSearchOption(ReverseSearchParameter.COUNTRY_CODETYPE, cboRevCOUNTRY_CODETYPE));
                        if (tbxRevENGINE_TARGETSIZE.Text != "")
                            lstReverseSearchOptions.Add(buildReverseSearchOption(ReverseSearchParameter.ENGINE_TARGETSIZE, tbxRevENGINE_TARGETSIZE.Text));
                        if (tbxRevENGINE_TOLERANCE.Text != "")
                            lstReverseSearchOptions.Add(buildReverseSearchOption(ReverseSearchParameter.ENGINE_TOLERANCE, tbxRevENGINE_TOLERANCE.Text));
                        if (tbxRevRESULT_LANGUAGE.Text != "")
                            lstReverseSearchOptions.Add(buildReverseSearchOption(ReverseSearchParameter.RESULT_LANGUAGE, tbxRevRESULT_LANGUAGE.Text));
                        if (tbxENGINE_FILTERMODE.Text != "")
                            lstReverseSearchOptions.Add(buildReverseSearchOption(ReverseSearchParameter.ENGINE_FILTERMODE, tbxENGINE_FILTERMODE.Text));
                        if (tbxENGINE_SEARCHDETAILLEVEL.Text != "")
                            lstReverseSearchOptions.Add(buildReverseSearchOption(ReverseSearchParameter.ENGINE_SEARCHDETAILLEVEL, tbxENGINE_SEARCHDETAILLEVEL.Text));
                        if (tbxENGINE_SEARCHRANGE.Text != "")
                            lstReverseSearchOptions.Add(buildReverseSearchOption(ReverseSearchParameter.ENGINE_SEARCHRANGE, tbxENGINE_SEARCHRANGE.Text));
                    }

                    startTime = DateTime.Now;
                    combinedTransportResponse = locateService.findCombinedTransportByLocation(inputLocation, lstReverseSearchOptions.ToArray(), resultField_CombinedTransport, cc);
                    displayTransactionTime(startTime);
                }
                else if (evtButton.Equals(btnFindObjectByText))
                {
                    List<XServer.SearchOption> searchOptionList = new List<XServer.SearchOption>();

                    if (singleFieldSeparatorsTxtBx.Text != "")
                    {
                        searchOptionList.Add(buildSearchOption(SearchParameter.SINGLE_FIELD_SEPARATORS, singleFieldSeparatorsTxtBx.Text));
                    }
                    searchOptionList.Add(buildSearchOption(SearchParameter.ENGINE_COMBINEDTRANSPORTSEARCH_ENABLE, cbxEngineCombinedTransportEnabled.Checked.ToString()));
                    searchOptionList.Add(buildSearchOption(SearchParameter.ENGINE_ADDRESSSEARCH_ENABLE, cbxEngineAddressEnable.Checked.ToString()));

                    // TODO check if it is usefull to also add the other search options
                    //if (cbxSearchOptions.Checked)
                    //{
                    //    searchOptionList.Add(buildSearchOption(SearchParameter.SEARCH_BINARY, cbxSEARCH_BINARY.Checked ? "1" : "0"));
                    //    searchOptionList.Add(buildSearchOption(SearchParameter.SEARCH_PHONETIC, cbxSEARCH_PHONETIC.Checked ? "1" : "0"));
                    //    searchOptionList.Add(buildSearchOption(SearchParameter.SEARCH_FUZZY, cbxSEARCH_FUZZY.Checked ? "1" : "0"));
                    //    searchOptionList.Add(buildSearchOption(SearchParameter.STREET_RETURNALLHNR, cbxSTREET_RETURNALLHNR.Checked ? "1" : "0"));
                    //    searchOptionList.Add(buildSearchOption(SearchParameter.CITY_RETURNALLCITY2, cbxCITY_RETURNALLCITY2.Checked ? "1" : "0"));
                    //    searchOptionList.Add(buildSearchOption(SearchParameter.MULTIWORDINDEX_ENABLE, cbxMULTIWORDINDEX_ENABLE.Checked ? "1" : "0"));
                    //    searchOptionList.Add(buildSearchOption(SearchParameter.POSTCODE_AGGREGATE, cbxPOSTCODE_AGGREGATE.Checked ? "1" : "0"));
                    //    searchOptionList.Add(buildSearchOption(SearchParameter.INTERSECTIONS_ENABLE, cbxINTERSECTIONS_ENABLE.Checked ? "1" : "0"));
                    //    searchOptionList.Add(buildSearchOption(SearchParameter.ASTERISKMODE, cboASTERISKMODE));
                    //    searchOptionList.Add(buildSearchOption(SearchParameter.COUNTRY_CODETYPE, cboCOUNTRY_CODETYPE));
                    //    searchOptionList.Add(buildSearchOption(SearchParameter.STREET_HNRPOSITION, cboSTREET_HNRPOSITION));
                    //    searchOptionList.Add(buildSearchOption(SearchParameter.SWAPANDSPLITMODE, cboSWAPANDSPLITMODE));
                    //    searchOptionList.Add(buildSearchOption(SearchParameter.HNR_OFFSET, tbxHNR_OFFSET.Text));
                    //    searchOptionList.Add(buildSearchOption(SearchParameter.RESULT_LANGUAGE, tbxRESULT_LANGUAGE.Text));
                    //    //
                    //    if (tbxMAX_RESULT.Text != "")
                    //        searchOptionList.Add(buildSearchOption(SearchParameter.MAX_RESULT, tbxMAX_RESULT.Text));
                    //    //
                    //}

                    startTime = DateTime.Now;
                    objectResponse = locateService.findObjectByText(singleFieldTextTxtBx.Text, singleFieldCountryTxtBx.Text, searchOptionList.ToArray(), null, resultField_AddressByText, cc);
                    displayTransactionTime(startTime);
                }

                // colapse panel based on the results
                resultSplitContainer.Panel1Collapsed = (addressResponse == null && objectResponse == null);
                resultSplitContainer.Panel2Collapsed = (combinedTransportResponse == null && objectResponse == null);

                if (addressResponse != null)
                {
                    resultAddresses = addressResponse.wrappedResultList;
                    tbxRES_COUNT.Text = resultAddresses.Length.ToString();
                    dgvResultAddresses.DataSource = resultAddresses;
                    dgvResultAddresses.Update();
                    if (addressResponse.errorCode != 0)
                    {
                        System.Windows.Forms.MessageBox.Show(this, addressResponse.errorDescription, "AddressResponse.ErrorDescription");
                    }
                    if (resultAddresses.Length > 0)
                        createToolTips(dgvResultAddresses, addressResponse.wrappedResultList[0].wrappedAdditionalFields);
                }
                if (combinedTransportResponse != null)
                {
                    resultCombinedTransports = combinedTransportResponse.wrappedResultList;
                    tbxRES_COUNT.Text = resultCombinedTransports.Length.ToString();
                    dgvResultCombinedTransport.DataSource = resultCombinedTransports;
                    dgvResultCombinedTransport.Update();
                    if (combinedTransportResponse.errorCode != 0)
                    {
                        System.Windows.Forms.MessageBox.Show(this, combinedTransportResponse.errorDescription, "CombinedTransportResponse.ErrorDescription");
                    }
                    if (resultCombinedTransports.Length > 0)
                        createToolTips(dgvResultCombinedTransport, combinedTransportResponse.wrappedResultList[0].wrappedCombinedTransportFields);
                }
                if (objectResponse != null)
                {
                    List<ResultCombinedTransport> resultCombinedTransportList = new List<ResultCombinedTransport>();
                    List<ResultAddress> resultAddressList = new List<ResultAddress>();
                    foreach (var resultObect in objectResponse.wrappedResultList)
                    {
                        if (resultObect.combinedTransport != null) resultCombinedTransportList.Add(resultObect.combinedTransport);
                        if (resultObect.address != null) resultAddressList.Add(resultObect.address);
                    }

                    dgvResultAddresses.DataSource = resultAddressList;
                    dgvResultAddresses.Update();
                    dgvResultCombinedTransport.DataSource = resultCombinedTransportList;
                    dgvResultCombinedTransport.Update();

                    if (objectResponse.errorCode != 0)
                    {
                        System.Windows.Forms.MessageBox.Show(this, objectResponse.errorDescription, "ObjectResponse.ErrorDescription");
                    }

                    if (resultCombinedTransportList.Count > 0)
                        createToolTips(dgvResultCombinedTransport, resultCombinedTransportList[0].wrappedCombinedTransportFields);
                    if (resultAddressList.Count > 0)
                        createToolTips(dgvResultAddresses, resultAddressList[0].wrappedAdditionalFields);

                }

                evtButton.BackColor = System.Drawing.Color.Green;
                evtButton.Update();
            }
            catch (System.Web.Services.Protocols.SoapException soapEx)
            {
                if ((soapEx.InnerException != null) && (soapEx.InnerException.Message != null))
                    exMessage = "xLocate: " + soapEx.InnerException.Message;
                else
                    exMessage = "xLocate: " + soapEx.Message;
            }
            //catch (System.InvalidOperationException ex)
            //{
            //    exMessage = "xLocate: " + ex.InnerException.Message;
            //}
            catch (Exception ex)
            {
                exMessage = "xLocate: " + ex.Message;
            }

            if (exMessage != "")
            {
                MessageBox.Show(exMessage);
                evtButton.BackColor = System.Drawing.Color.Red;
                dgvResultAddresses.DataSource = null;
                dgvResultAddresses.Update();
            }
            else
            {
                if ((resultAddresses != null) && (resultAddresses.Length > 0))
                {
                    // Prepare CustomLayer for centering Objects 
                    XServer.Bitmap[] bitmap = new XServer.Bitmap[resultAddresses.Length];
                    for (int i = 0; i < resultAddresses.Length; i++)
                    {
                        bitmap[i] = new XServer.Bitmap();
                        bitmap[i].descr = displayResultAddress(resultAddresses[i]);
                        switch (resultAddresses[i].detailLevelDescription)
                        {
                            case DetailLevelDescription.HNREXACT: bitmap[i].name = "flaggreen.bmp"; break;
                            case DetailLevelDescription.HNRINTERPOLATED: bitmap[i].name = "flagred.bmp"; break;
                            default: bitmap[i].name = "location.bmp"; break;
                        }

                        XServer.Point point = new XServer.Point();
                        if (resultAddresses[i].coordinates.wkb != null)
                        {
                            point.wkb = resultAddresses[i].coordinates.wkb;
                        }
                        else if (resultAddresses[i].coordinates.wkt != null)
                        {
                            point.wkt = resultAddresses[i].coordinates.wkt;
                        }
                        else if (resultAddresses[i].coordinates.kml != null)
                        {   //2010.11.15
                            point.kml = new KML();
                            point.kml.kml = resultAddresses[i].coordinates.kml.kml;
                            point.kml.wrappedPlacemarks = resultAddresses[i].coordinates.kml.wrappedPlacemarks;
                        }
                        else
                        {
                            point.point = new PlainPoint()
                            {
                                x = resultAddresses[i].coordinates.point.x,
                                y = resultAddresses[i].coordinates.point.y
                            };
                        }
                        bitmap[i].position = point;
                    }

                    Bitmaps bitmaps = new Bitmaps()
                    {
                        wrappedBitmaps = bitmap
                    };

                    customLayer.wrappedBitmaps = new Bitmaps[] { bitmaps };
                    customLayer.centerObjects = true;
                    lstLayer.Add(customLayer);
                    // 2011.12.09: GeometryLayer for the LINESTRINGXYN

                    GeometryLayer glXYN = getXYNLayer(resultAddresses);
                    if (glXYN != null)
                        lstLayer.Add(glXYN);

                }

                // 2011-12-21 Migration combinedTransport
                if (resultCombinedTransports != null)
                {
                    List<LineString> lstLineString = new List<LineString>();
                    List<XServer.Bitmap> lstBitmapEndpoint = new List<XServer.Bitmap>();
                    foreach (ResultCombinedTransport curResultCombinedTransport in resultCombinedTransports)
                    {
                        // collect the connection lines...
                        PlainLineString curPlainLineString = new PlainLineString()
                        {
                            wrappedPoints = new PlainPoint[]{
                                curResultCombinedTransport.start.coordinate.point,
                                curResultCombinedTransport.destination.coordinate.point
                            }
                        };
                        lstLineString.Add(new LineString()
                        {
                            lineString = curPlainLineString
                        });
                        //... and the endpoints
                        lstBitmapEndpoint.Add(new XServer.Bitmap()
                        {
                            descr = curResultCombinedTransport.start.ToString(),
                            name = Properties.Settings.Default.COMBINEDTRANSPORTLAYER_START,
                            position = curResultCombinedTransport.start.coordinate
                        });
                        lstBitmapEndpoint.Add(new XServer.Bitmap()
                        {
                            descr = curResultCombinedTransport.destination.ToString(),
                            name = Properties.Settings.Default.COMBINEDTRANSPORTLAYER_DESTINATION,
                            position = curResultCombinedTransport.destination.coordinate
                        });

                    }
                    Lines curLines = new Lines()
                    {
                        wrappedLines = lstLineString.ToArray(),
                        options = new LineOptions()
                        {
                            transparent = Properties.Settings.Default.COMBINEDTRANSPORTLAYER_TRANSPARENCY,
                            showFlags = false,
                            mainLine = new LinePartOptions()
                            {
                                width = Properties.Settings.Default.COMBINEDTRANSPORTLAYER_LINEWIDTH,
                                visible = true
                            }
                        }
                    };
                    Bitmaps curBitmaps = new Bitmaps()
                    {
                        wrappedBitmaps = lstBitmapEndpoint.ToArray()
                    };

                    CustomLayer clCombinedTransport = new CustomLayer()
                    {
                        centerObjects = true,
                        drawPriority = Properties.Settings.Default.COMBINEDTRANSPORTLAYER_PRIORITY,
                        name = Properties.Settings.Default.COMBINEDTRANSPORTLAYER_NAME,
                        objectInfos = ObjectInfoType.REFERENCEPOINT,
                        visible = true,
                        wrappedLines = new Lines[] { curLines },
                        wrappedBitmaps = new Bitmaps[] { curBitmaps }
                    };

                    lstLayer.Add(clCombinedTransport);
                    lstLayer.Add(new StaticPoiLayer() { name = "combinedtransports", visible = true, category = -1, objectInfos = ObjectInfoType.REFERENCEPOINT });
                }


                if ((lstLayer.Count > 0) && (tbxServiceAddressMap.Text != "") && (cbxDisplayMap.Checked))
                {
                    MapForm mapForm = new MapForm(tbxServiceAddressMap.Text, lstLayer.ToArray(), (cc != null) ? (getSelectedString(cboCoordFormat)) : (null), tbxMapProfile.Text);
                    mapForm.Show(this);
                }

                evtButton.BackColor = System.Drawing.Color.Green;
            }
            evtButton.Update();

            return;
        }

        /**
         *  2011.12.09: The XYN layer is used for the street segments. Requires additional data
         */
        public GeometryLayer getXYNLayer(ResultAddress[] resultAddress)
        {
            List<GeometryExt> lstGeometryExt = new List<GeometryExt>();
            if (resultAddress != null)
            {
                for (int i = 0; i < resultAddress.Length; i++)
                {
                    ResultAddress curResultAddress = resultAddress[i];
                    foreach (AdditionalField curAdditionalField in curResultAddress.wrappedAdditionalFields)
                    {
                        if (curAdditionalField.field == ResultField.XYN)
                        {
                            if (curAdditionalField.value != "LINESTRINGXYN(0 0 0 0 0, 0 0 0 0 0)") // indicates an invalid geometry...
                            {
                                GeometryExt geometryExt = new GeometryExt()
                                {
                                    description = displayResultAddress(curResultAddress),
                                    geometry = null,
                                    geometryString = curAdditionalField.value,
                                    id = i,
                                    referencePoint = curResultAddress.coordinates,
                                };
                                lstGeometryExt.Add(geometryExt);
                            }
                        }
                    }
                }
                if (lstGeometryExt.Count > 0)
                {
                    GeometryLayer glGeometryLayer = new GeometryLayer()
                    {
                        drawPriority = Properties.Settings.Default.XYNLAYER_PRIORITY,
                        name = Properties.Settings.Default.XYNLAYER_NAME,
                        objectInfos = ObjectInfoType.REFERENCEPOINT,
                        visible = Properties.Settings.Default.XYNLAYER_VISIBLE,
                        wrappedGeometries = new Geometries[]
                        {
                            new Geometries(){
                            wrappedGeometries = lstGeometryExt.ToArray(),
                            wrappedOptions = new GeometryOption[]{
                                new GeometryOption(){option=GeometryOptions.LINECOLOR,value=Properties.Settings.Default.XYNLAYER_LINECOLOR},
                                new GeometryOption(){option=GeometryOptions.LINEWIDTH,value=Properties.Settings.Default.XYNLAYER_LINEWIDTH},
                                new GeometryOption(){option=GeometryOptions.LINEALPHA,value=Properties.Settings.Default.XYNLAYER_LINEALPHA}
                                }
                            }
                        }
                    };
                    return glGeometryLayer;
                }
            }
            return null;
        }

        public XServer.SearchOption buildSearchOption(XServer.SearchParameter argParameter, string argValue)
        {
            return new XServer.SearchOption() { param = argParameter, value = argValue };
        }

        public XServer.SearchOption buildSearchOption(XServer.SearchParameter argParameter, ComboBox cboARG)
        {
            return new XServer.SearchOption() { param = argParameter, value = getSelectedString(cboARG) };
        }

        public ReverseSearchOption buildReverseSearchOption(ReverseSearchParameter argParameter, string argValue)
        {
            return new ReverseSearchOption()
            {
                param = argParameter,
                value = argValue
            };
        }

        public ReverseSearchOption buildReverseSearchOption(ReverseSearchParameter argParameter, ComboBox cboARG)
        {
            return buildReverseSearchOption(argParameter, getSelectedString(cboARG));
        }

        public string getSelectedString(ComboBox cboARG)
        {
            string strResult;
            try
            {
                String theSelectedString = cboARG.SelectedItem.ToString();
                //strResult = theSelectedString.Substring(0, theSelectedString.IndexOf(" "));
                strResult = theSelectedString.Split('-')[0].Trim();
            }
            catch (Exception)
            {
                strResult = "";
            }
            return strResult;
        }

        public void fillCBO(ComboBox theCBO, string[] args)
        {

            foreach (string curStr in args)
                theCBO.Items.Add(curStr);
            theCBO.SelectedIndex = 0;
        }

        public void resetButtons()
        {
            btnFindAddress.BackColor = System.Drawing.Color.White;
            btnFindAddress.Update();
            btnFindLocation.BackColor = System.Drawing.Color.White;
            btnFindLocation.Update();
            btnFindAddressByText.BackColor = System.Drawing.Color.White;
            btnFindAddressByText.Update();
            // 2011-12-21 findCombinedTransport...
            btnFindCombinedTransportByLocation.BackColor = System.Drawing.Color.White;
            btnFindCombinedTransportByLocation.Update();
        }

        private void cbxSearchOptions_CheckedChanged(object sender, EventArgs e)
        {
            bool myBool = cbxSearchOptions.Checked;
            cbxSEARCH_BINARY.Enabled = myBool;
            cbxSEARCH_PHONETIC.Enabled = myBool;
            cbxSEARCH_FUZZY.Enabled = myBool;
            cbxSTREET_RETURNALLHNR.Enabled = myBool;
            cbxCITY_RETURNALLCITY2.Enabled = myBool;
            cbxMULTIWORDINDEX_ENABLE.Enabled = myBool;
            cbxPOSTCODE_AGGREGATE.Enabled = myBool;
            cbxINTERSECTIONS_ENABLE.Enabled = myBool;
            //
            cboCOUNTRY_CODETYPE.Enabled = myBool;
            cboSWAPANDSPLITMODE.Enabled = myBool;
            cboSTREET_HNRPOSITION.Enabled = myBool;
            cboASTERISKMODE.Enabled = myBool;
            //
            tbxRESULT_LANGUAGE.Enabled = myBool;
            tbxHNR_OFFSET.Enabled = myBool;
            tbxMAX_RESULT.Enabled = myBool;
        }

        private void cbxReverseSearchOptions_CheckedChanged(object sender, EventArgs e)
        {
            bool myBool = cbxReverseSearchOptions.Checked;
            cboRevCOUNTRY_CODETYPE.Enabled = myBool;
            tbxRevRESULT_LANGUAGE.Enabled = myBool;
            tbxRevENGINE_TOLERANCE.Enabled = myBool;
            tbxRevENGINE_TARGETSIZE.Enabled = myBool;
            tbxENGINE_FILTERMODE.Enabled = myBool;
            //2011-04-04
            tbxENGINE_SEARCHDETAILLEVEL.Enabled = myBool;
            tbxENGINE_SEARCHRANGE.Enabled = myBool;
        }

        private void cbxCallerContextProperties_CheckedChanged(object sender, EventArgs e)
        {
            bool myBool = cbxCallerContextProperties.Checked;
            cboResponseGeometry.Enabled = myBool;
            cboCoordFormat.Enabled = myBool;
            tbxProfile.Enabled = myBool;
        }

        private string displayResultAddress(ResultAddress resultAddres)
        {
            string label = resultAddres.totalScore + ":";
            label += resultAddres.country;
            if (resultAddres.postCode != "")
                label += ((resultAddres.country != "") ? ("-") : ("")) + resultAddres.postCode;
            if (resultAddres.state != "")
                label += " (" + resultAddres.state + ")";
            if (resultAddres.city != "")
                label += " " + resultAddres.city;
            if (resultAddres.city2 != "")
                label += "/" + resultAddres.city2;
            if (resultAddres.street != "")
                label += "," + resultAddres.street;
            if (resultAddres.houseNumber != "")
                label += " " + resultAddres.houseNumber;
            return label;
        }

        private void createToolTips(DataGridView dgv, AdditionalField[] af)
        {
            for (int rowIDX = 0; rowIDX < dgv.Rows.Count; rowIDX++)
            {
                DataGridViewRow row = dgv.Rows[rowIDX];
                string tooltipText = "";
                //ResultObject ro = objectResponse.wrappedResultList[rowIDX];
                //(typeof(roArray))roArray[rowIDX];                
                //ResultAddress ra = resultAddress[rowIDX];
                //AdditionalField[] af = ra.wrappedAdditionalFields;
                if (af != null)
                {
                    foreach (AdditionalField curAF in af)
                    {
                        tooltipText += curAF.field.ToString() + " = " + curAF.value + "\r\n";
                    }
                    tooltipText += "\r\n";
                    tooltipText = tooltipText.Replace("\r\n\r\n", "");
                }
                else
                {
                    tooltipText = "no additional fields returned from server";
                }
                for (int colIDX = 0; colIDX < row.Cells.Count; colIDX++)
                {
                    row.Cells[colIDX].ToolTipText = tooltipText;
                }
            }
        }

        private void createResultField_Location()
        {
            resultField_Location = new ResultField[]{
                ResultField.SEGMENT_ID,
                ResultField.SEGMENT_COUNTRY,
                ResultField.SEGMENT_DIRECTION,
                ResultField.XYN,
                ResultField.POPULATION,
                ResultField.CITY,
            };
        }

        private void createResultField_AddressByText()
        {
            resultField_AddressByText = new ResultField[]{
                ResultField.UNMATCHED_WORDS,
                ResultField.UNMATCHED_WORDS_COUNT
            };
        }

        // 2011-12-21 CombinedTransport search...
        private void createResultField_CombinedTransport()
        {
            resultField_CombinedTransport = new ResultField[]{
                ResultField.DESTINATION_LOCATION_COUNTRYCODE,
                ResultField.DESTINATION_LOCATION_NODE_N,
                ResultField.DESTINATION_LOCATION_NODE_X,
                ResultField.DESTINATION_LOCATION_NODE_Y,
                ResultField.DESTINATION_LOCATION_TILE_X,
                ResultField.DESTINATION_LOCATION_TILE_Y,
                ResultField.START_LOCATION_COUNTRYCODE,
                ResultField.START_LOCATION_NODE_N,
                ResultField.START_LOCATION_NODE_X,
                ResultField.START_LOCATION_NODE_Y,
                ResultField.START_LOCATION_TILE_X,
                ResultField.START_LOCATION_TILE_Y,
                ResultField.ISBLOCKEDFORBICYCLES,
                ResultField.ISBLOCKEDFORBUSES,
                ResultField.ISBLOCKEDFORCAMPERS,
                ResultField.ISBLOCKEDFORCARS,
                ResultField.ISBLOCKEDFORCOMBUSTIBLEGOODS,
                ResultField.ISBLOCKEDFORHAZARDOUSGOODS,
                ResultField.ISBLOCKEDFORMOTORCYCLES,
                ResultField.ISBLOCKEDFORPEDESTRIANS,
                ResultField.ISBLOCKEDFORTRUCKS,
                ResultField.ISBLOCKEDFORVANS
            };
        }

        private void createResultField_Address()
        {
            List<ResultField> lstResultField = new List<ResultField>();
            lstResultField.AddRange(new ResultField[]{
                ResultField.TOWN_CLASSIFICATION,
                ResultField.POSTCODE_CLASSIFICATION,
                ResultField.STREET_CLASSIFICATION,
                ResultField.HOUSENR_CLASSIFICATION,
                ResultField.CLASSIFICATION,
                ResultField.CLASSIFICATION_DESCRIPTION,
                ResultField.ADDRESS_CLASSIFICATION,
                ResultField.ADDRESS_CLASSIFICATION_DESCRIPTION,
                ResultField.COUNTRY,
                ResultField.STATE,
                ResultField.ADMINREGION,
                ResultField.CITY,
                ResultField.CITY2,
                ResultField.POSTCODE,
                ResultField.STREET,
                ResultField.HOUSENR,
                ResultField.COORDX,
                ResultField.COORDY,
                ResultField.DETAILLEVEL,
                ResultField.DETAILLEVEL_DESCRIPTION,
                ResultField.POPULATION,
                ResultField.EXTENSIONCLASS,
                ResultField.LEVEL,
                ResultField.ISCITYDISTRICT,
                ResultField.COUNTRY_ISO2,
                ResultField.COUNTRY_ISO3,
                ResultField.COUNTRY_COUNTRYCODEPLATE,
                ResultField.COUNTRY_DIALINGCODE,
                ResultField.COUNTRY_CAPITAL,
                ResultField.COUNTRY_NAME,
                ResultField.HOUSENR_SIDE,
                ResultField.HOUSENR_STRUCTURE,
                ResultField.HOUSENR_STARTFORMAT,
                ResultField.HOUSENR_ENDFORMAT,
                ResultField.APPENDIX,
                ResultField.SCORE_TOTALSCORE,
                ResultField.SCORE_FINALPENALTY,
                ResultField.FOUNDBY_CITY,
                ResultField.FOUNDBY_CITY2,
                ResultField.FOUNDBY_POSTCODE,
                ResultField.FOUNDBY_STREET,
                ResultField.SWAPANDSPLITMODE
            });
            if (cbxUseNewFields.Checked)
            {
                lstResultField.AddRange(
                     new ResultField[]
                    {   // new since 1.10
                        ResultField.ADDRESS_CLASSIFICATION,
                        ResultField.ADDRESS_CLASSIFICATION_DESCRIPTION,
                        ResultField.POSTCODE_CLASSIFICATION,
                        ResultField.TOWN_CLASSIFICATION,
                        ResultField.STREET_CLASSIFICATION,
                        ResultField.HOUSENR_CLASSIFICATION,
                        ResultField.POSTCODE_CHARACTERISTICS,
                        ResultField.TOWN_CHARACTERISTICS,
                        ResultField.STREET_CHARACTERISTICS,
                        ResultField.HOUSENR_CHARACTERISTICS,
                        ResultField.STREETX,
                        ResultField.STREETY,
                        ResultField.ADMINX,
                        ResultField.ADMINY
                    });
            }
            resultField_Address = lstResultField.ToArray();
        }

        private ResultAddress[] filter2city(ResultAddress[] inputAddress)
        {
            List<ResultAddress> lstResultAddress = new List<ResultAddress>();
            foreach (ResultAddress curRA in inputAddress)
            {
                if (curRA.detailLevelDescription == DetailLevelDescription.CITY)
                    lstResultAddress.Add(curRA);
            }
            return lstResultAddress.ToArray();
        }

        private void dgvResultAddresses_CellMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            // right click on result address updates the input text boxes with
            // the selected address properties
            if (e.Button == MouseButtons.Right)
            {
                var resultAddress = (ResultAddress)((DataGridView)sender).Rows[e.RowIndex].DataBoundItem;

                tbxCountry.Text = resultAddress.country;
                tbxState.Text = resultAddress.state;
                tbxPostCode.Text = resultAddress.postCode;
                tbxCity.Text = resultAddress.city;
                tbxCity2.Text = resultAddress.city2;
                tbxStreet.Text = resultAddress.street;
                tbxHouseNumber.Text = resultAddress.houseNumber;
                //
                tbxLocationX.Text = resultAddress.coordinates.point.x.ToString();
                tbxLocationY.Text = resultAddress.coordinates.point.y.ToString();
                //
                singleFieldTextTxtBx.Text = displaySingleFieldAddress(resultAddress);
                singleFieldCountryTxtBx.Text = resultAddress.country;
                var segmentField = resultAddress.wrappedAdditionalFields.FirstOrDefault(x => x.field == ResultField.SEGMENT_ID);
                if (segmentField != null)
                    Clipboard.SetText(segmentField.value);
            }
        }

        private string displaySingleFieldAddress(Address adr)
        {
            string result = "";
            if (adr.postCode != "")
                result += adr.postCode;
            if (adr.city != "")
                result += " " + adr.city;
            if (adr.city2 != "")
                result += " " + adr.city2;
            if (adr.street != "")
            {
                result += singleFieldSeparatorsTxtBx.Text + adr.street;
                if (adr.houseNumber != "")
                    result += " " + adr.houseNumber;
            }
            return result;

        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            createResultField_Address();
        }

        private void displayTransactionTime(DateTime startTime)
        {
            TimeSpan span = DateTime.Now.Subtract(startTime);
            taCur = span.TotalSeconds;
            taTotal += taCur;
            taCount++;
            if (taCount == 1)
            {
                taMin = taCur;
                taMax = taCur;
                taAvg = taCur;
            }
            else
            {
                taAvg = taTotal / taCount;
                if (taCur < taMin)
                    taMin = taCur;
                if (taCur > taMax)
                    taMax = taCur;
            }

            tbxTAavg.Text = taAvg.ToString();
            tbxTAcurr.Text = taCur.ToString();
            tbxTAmin.Text = taMin.ToString();
            tbxTAmax.Text = taMax.ToString();
        }

        private string getAdditionalField(ResultCombinedTransport resultCT, ResultField resultField)
        {
            string result = "";
            if (resultCT.wrappedCombinedTransportFields != null)
            {
                foreach (AdditionalField curAF in resultCT.wrappedCombinedTransportFields)
                {
                    if (curAF.field == resultField)
                        result = curAF.value;
                }
            }

            return result;
        }

        //private void dgvResultAddresses_CellMouseClick_1(object sender, DataGridViewCellMouseEventArgs e)
        //{
        //    if (e.Button == System.Windows.Forms.MouseButtons.Right)
        //    {

        //    }
        //}
    }
}