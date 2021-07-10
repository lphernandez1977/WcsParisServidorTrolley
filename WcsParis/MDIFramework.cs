using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using S7.Net;
using System.Configuration;
using System.Threading;
using System.Net;
using System.Net.NetworkInformation;
using System.IO.Ports;
using System.Text.RegularExpressions;
using System.Reflection;

namespace WcsParis
{
    public partial class MDIFramework : Form
    {
        //PLC
        private Plc oPLC = null;

        private cEntidades.cEnums.ExceptionCode errorcode;
        bool EstadoPLC = false;
        bool stop = true;
        bool stopHora = false;
        bool EstadoTMP = false;
        bool EstadoTMPHora = false;
        bool spFlag = false;
        bool spEscribeString = false;
        bool spEscribeEntero = false;
        string COM = string.Empty;

        //registro log
        cRegistroErr RegErrores = new cRegistroErr();

        //ping
        private cPing ping = new cPing();

        //timer plc
        private static System.Threading.Timer temporizador = null;

        #region ENTIDADES
        public cDispositivos oDirecciones = new cDispositivos();
        public cTagPlc oTagPLC = new cTagPlc();
        private cEnt_Server oServer = new cEnt_Server();
        private cTb_Lectura_Cartones oLecturaCartones = new cTb_Lectura_Cartones();
        #endregion

        #region LOGICA
        private cLogica.cServer lServer = new cLogica.cServer();
        private LGN_ListadoSensores _LGN_ListadoSensores = new LGN_ListadoSensores();
        private LGN_Insertar_Lecturas _LGN_Insertar_Lecturas = new LGN_Insertar_Lecturas();
        #endregion

        //utilizamos la clase para saltar al siguiente control
        CLS_Utilidades.CLS_SeteaFormatoGrilla _seteaformato = new CLS_Utilidades.CLS_SeteaFormatoGrilla();
        CLS_Utilidades.CLS_Ilumina_Texto _iluminaTexto = new CLS_Utilidades.CLS_Ilumina_Texto();

        //Grilla
        #region
        //**// Crea la Alineacion de la grilla
        enum Alineacion
        {
            Izquierda,
            Centro,
            Derecha
        }

        //**// Limpiar grilla
        private void GrillaLimpia(DataGridView dtg)
        {
            dtg.DataSource = null;
            dtg.Rows.Clear();
            dtg.Columns.Clear();

            _seteaformato.CreaGrillaLimpia(dtg, 0, "Id", true);
            _seteaformato.CreaGrillaLimpia(dtg, 1, "Carton LPN", true);
            _seteaformato.CreaGrillaLimpia(dtg, 2, "Salida", true);
            _seteaformato.CreaGrillaLimpia(dtg, 3, "Fecha", true);
            _seteaformato.CreaGrillaLimpia(dtg, 4, "Estado", true);
            //_seteaformato.CreaGrillaLimpia(dtg, 3, "Estado_Usuario", true);
            //_seteaformato.CreaGrillaLimpia(dtg, 4, "Fecha_Crea", true);
            //_seteaformato.CreaGrillaLimpia(dtg, 5, "Contraseña", true);
        }

        private void FormatoGrilla(DataGridView dtg)
        {

            //***********************
            //CABECERA DE GRILLA
            //***********************
            //Permite habilitar los formatos del usuario
            DgvDatos.EnableHeadersVisualStyles = false;
            //oculta la columna Default de grilla
            DgvDatos.RowHeadersVisible = false;

            _seteaformato.SeteaFormatoGrilla(dtg, 0, "Id", 50, true, Alineacion.Izquierda.ToString(), true);
            _seteaformato.SeteaFormatoGrilla(dtg, 1, "Carton LPN", 150, true, Alineacion.Izquierda.ToString(), true);
            _seteaformato.SeteaFormatoGrilla(dtg, 2, "Salida", 50, true, Alineacion.Centro.ToString(), true);
            _seteaformato.SeteaFormatoGrilla(dtg, 3, "Fecha", 100, true, Alineacion.Izquierda.ToString(), true);
            _seteaformato.SeteaFormatoGrilla(dtg, 4, "Estado", 150, true, Alineacion.Izquierda.ToString(), true);
            //_seteaformato.SeteaFormatoGrilla(dtg, 4, "Fecha_Crea", 100, false, Alineacion.Izquierda.ToString(), true);
            //_seteaformato.SeteaFormatoGrilla(dtg, 5, "Contraseña", 100, false, Alineacion.Izquierda.ToString(), true);

            //--- Permite la Seleccion de la Fila Completa
            DgvDatos.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            //--- Permite que la grilla no se pueda editar
            DgvDatos.ReadOnly = true;
            //--- desabilita el agrear linea
            DgvDatos.AllowUserToAddRows = false;
            //Permite copiar al Portapapeles (Memoria), para luego pegar... excel, bloc de notas, etc. (solo Dios y SG sabe lo que hace)
            DgvDatos.ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableAlwaysIncludeHeaderText;
            DgvDatos.ColumnHeadersDefaultCellStyle.ForeColor = Color.Navy;

            //***********************
            //DETALLE DE GRILLA
            //***********************
            //cambia el Color de Fondo de la Grilla
            DgvDatos.DefaultCellStyle.BackColor = Color.FromArgb(255, 255, 192);
            //cambia el Color de Seleccion
            DgvDatos.DefaultCellStyle.SelectionBackColor = Color.Lime;
            //cambia el Color de fuente
            DgvDatos.DefaultCellStyle.SelectionForeColor = Color.Navy;
            //cambia el Tamaño y Letra de la fuente, color 
            DgvDatos.DefaultCellStyle.Font = new System.Drawing.Font("Arial", 8);
            DgvDatos.DefaultCellStyle.ForeColor = Color.Navy;
            DgvDatos.MultiSelect = false;

        }
        #endregion

        #region Temporizador HORA PROGRAMA

        private void OnStart()
        {
            stop = false;
            TimerCallback tcb = new TimerCallback(OnElapsedTime);
            temporizador = new System.Threading.Timer(tcb, null, 1000, 1000);
            EstadoTMP = true;
        }

        private void OnElapsedTime(Object stateInfo)
        {
            FechaHoraServer();
        }

        private void OnStop()
        {
            stop = true;
            temporizador.Dispose();
            EstadoTMP = false;
        }
        #endregion

        #region CONECTAR PLC
        private void cConPLC()
        {
            try
            {
                oPLC = new Plc(CpuType.S71200, oDirecciones.IpPLC, oDirecciones.RackPLC, oDirecciones.SlotPLC);

                oPLC.Open();

                EstadoPLC = oPLC.IsConnected;

                if (EstadoPLC)
                {
                    this.ToolEstadoPlc.Text = "Conectado";
                    ToolEstadoPlc.ForeColor = Color.Green;

                }
                else
                {
                    this.ToolEstadoPlc.Text = "Desconectado";
                    ToolEstadoPlc.ForeColor = Color.Red;
                }

            }
            catch (Exception ex)
            {
                //MessageBox.Show(ex.Message.ToString(), "Información", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.ToolEstadoPlc.Text = "Desconectado";
                ToolEstadoPlc.ForeColor = Color.Red;
                RegErrores.RegistroLog(ex.Message.ToString());
            }
        }               
        #endregion

        #region LEER_PLC - ECCRIBIR PLC

        private void EscribirSalida_1(short valor)
        {
            short db1IntVariableSalida;


            db1IntVariableSalida = valor;

            try
            {
                oPLC.Write("DB100.DBW0", db1IntVariableSalida.ConvertToUshort());
                //oPLC.Write("DB100.DBW2", db1IntVariableSalida.ConvertToUshort());                
            }
            catch (Exception err)
            {
                RegErrores.RegistroLog(err.Message.ToString() + " FUNCION SALIDA 1");
            }
        }

        private void EscribirSalida_2(short valor)
        {
            short db1IntVariableSalida;


            db1IntVariableSalida = valor;

            try
            {
                //oPLC.Write("DB100.DBW0", db1IntVariableSalida.ConvertToUshort());
                oPLC.Write("DB100.DBW2", db1IntVariableSalida.ConvertToUshort());                
            }
            catch (Exception err)
            {
                RegErrores.RegistroLog(err.Message.ToString() + " FUNCION SALIDA 2");
            }
        }

        private void EscribirTracking_1(int valor)
        {
            Int32 db1IntVariable;

            db1IntVariable = valor;

            try
            {

                oPLC.Write("DB100.DBD4", db1IntVariable.ConvertToUInt());
                //oPLC.Write("DB100.DBD8", db1IntVariable.ConvertToUInt());                
            }
            catch (Exception err) 
            {
                RegErrores.RegistroLog(err.Message.ToString() + "FUNCION TRACKING 2");
            }
        }

        private void EscribirTracking_2(int valor)
        {
            Int32 db1IntVariable;

            db1IntVariable = valor;

            try
            {

                //oPLC.Write("DB100.DBD4", db1IntVariable.ConvertToUInt());
                oPLC.Write("DB100.DBD8", db1IntVariable.ConvertToUInt());                
            }
            catch (Exception err)
            {
                RegErrores.RegistroLog(err.Message.ToString() + " FUNCION TRACKING 2");
            }
        }
              
        #endregion

        #region PUERTO SERIAL

        private void PuertoSerial()
        {
            try
            {
                if (spPuertoSerie.IsOpen) { spPuertoSerie.Close(); }

                spPuertoSerie.PortName = oDirecciones.PtoCOM;
                //spPuertoSerie.BaudRate = 9600;
                //spPuertoSerie.DataBits = 8;
                //spPuertoSerie.StopBits = StopBits.One;
                //spPuertoSerie.Parity = Parity.None;
                //spPuertoSerie.DtrEnable = false;
                //spPuertoSerie.Handshake =  Handshake.None;
                //spPuertoSerie.ReadBufferSize = 2048;
                //spPuertoSerie.WriteBufferSize = 1024;                               
                //spPuertoSerie.WriteTimeout = 500;
                //spPuertoSerie.Encoding = System.Text.Encoding.Default;                
                //abre puerto
                spPuertoSerie.Open();
                // Detecta cualquier dato recibido.
                spPuertoSerie.DataReceived += new SerialDataReceivedEventHandler(DataReceivedHandler);

                LblEstadoPuertoSerial.Text = oDirecciones.PtoCOM + " En Linea";
                LblEstadoPuertoSerial.ForeColor = Color.Green;
                spFlag = true;

            }
            catch (Exception ex)
            {
                LblEstadoPuertoSerial.Text = ex.Message.ToString();
                LblEstadoPuertoSerial.ForeColor = Color.Red;
                spFlag = false;
                RegErrores.RegistroLog(ex.Message.ToString() + " funcion PuertoSerial");
            }

        }

        // Detecta cualquier dato entrante.
        private void DataReceivedHandler(object sender, SerialDataReceivedEventArgs e)
        {
            string res = string.Empty;
            string Lectura = string.Empty;
            string eTiquetaFin = string.Empty;
            int largoCarton = 0;
            string entradaDatos = string.Empty;
            short loc_LineaSalida = 0;
            
            cTb_Lectura_Cartones cResTb = new cTb_Lectura_Cartones();

            try
            {
                SerialPort sp = (SerialPort)sender;
                entradaDatos = sp.ReadLine(); // Almacena los datos recibidos en la variable tipo string.  

                char[] MyChar = { '\u0002', '\u0003', '\u0010', '\u0013', '\r', '\n' };

                Lectura = entradaDatos.TrimStart(MyChar).TrimEnd(MyChar);

                //valor visualizar pantalla
                LblEtiquetaScan.Text = Lectura;

                if (Lectura == "NO READ")
                {
                    oLecturaCartones.CartonLPN = Lectura;
                }

                else
                {
                    eTiquetaFin = Lectura.Substring(0, 11).Trim();
                    largoCarton = Convert.ToInt32(eTiquetaFin.Length);

                    //asigno el objeto
                    oLecturaCartones.CartonLPN = eTiquetaFin;
                    oLecturaCartones.StatusUpdFlag = false;
                }

                //limpiar lectura serial.
                entradaDatos = string.Empty;

                //inserto la etiqueta
                cResTb = _LGN_Insertar_Lecturas.Inserta_Lecturas_Cartones(oLecturaCartones);

                //
                if (cResTb == null)
                {
                    //envio linea
                    EscribirSalida_1(99);

                    EscribirSalida_2(99);

                    //envio tracking id movimiento
                    EscribirTracking_1(cResTb.RecId);

                    EscribirTracking_2(cResTb.RecId);
                    
                    if (spEscribeString) { spEscribeString = false; }
                    
                    RegErrores.RegistroLog("Tienda " + eTiquetaFin + " sin linea asignada funcion DataReceivedHandler");
                }

                if (cResTb.Result == "0")
                {               
                    //envio linea
                    EscribirSalida_1(99);

                    EscribirSalida_2(99);

                    //envio tracking id movimiento
                    EscribirTracking_1(cResTb.RecId);

                    EscribirTracking_2(cResTb.RecId);
                                  
                    RegErrores.RegistroLog("Tienda " + eTiquetaFin + " sin linea asignada funcion DataReceivedHandler");
                }

                if (cResTb.Result == "1")
                {
                    if (string.IsNullOrEmpty(cResTb.Lane.ToString())) { cResTb.Lane = 99; }

                    //asigno linea de salida
                    loc_LineaSalida = Convert.ToInt16(cResTb.Lane);

                    //envio linea
                    EscribirSalida_1(loc_LineaSalida);

                    EscribirSalida_2(loc_LineaSalida);
                                                                             
                    //envio tracking id movimiento
                    EscribirTracking_1(cResTb.RecId);

                    EscribirTracking_2(cResTb.RecId);
                                   
                }

                entradaDatos = string.Empty;
            }
            catch (Exception ex)
            {                                 
                RegErrores.RegistroLog(ex.Message.ToString() + " funcion DataReceivedHandler");
            }
        }

        #endregion

        public MDIFramework()
        {
            InitializeComponent();
            Control.CheckForIllegalCrossThreadCalls = false;
        }

        private void MDIFramework_Load(object sender, EventArgs e)
        {

            FrmInicio frm = new FrmInicio();
            frm.MdiParent = this;
            frm.Show();

            //INICIAR TEMPORIZADOR HILO
            OnStart();

            LblEstado.Visible = false;

            PbStart.Visible = false;
            PbStop.Visible = false;

            BtnStart.Visible = false;
            BtnStop.Visible = false;

            //emergencias
            Emergencia1.Visible = false;
            Emergencia2.Visible = false;
            Emergencia3.Visible = false;

            LblEmer1.Visible = false;
            LblEmer2.Visible = false;
            LblEmer3.Visible = false;

            //Full Line
            FullLine1.Visible = false;
            FullLine2.Visible = false;
            FullLine3.Visible = false;

            FullLine1Est.Visible = false;
            FullLine2Est.Visible = false;
            FullLine3Est.Visible = false;


            Direcciones();

            Application.DoEvents();

            Application.DoEvents();

            Application.DoEvents();

            frm.Close();

            ToolEstadoPlc.Text = "Iniciando......";

            if (ping.PingServidores(oDirecciones.IpPLC))
            {
                cConPLC();
                this.ToolEstadoPlc.Text = "En Linea";
                ToolEstadoPlc.ForeColor = Color.Green;
            }
            else
            {
                this.ToolEstadoPlc.Text = "Desconectado";
                ToolEstadoPlc.ForeColor = Color.Red;
            }

            //FUNCION PUERTO SERIAL
            PuertoSerial();

            //INICIAR TEMPORIZADOR NORMAL PLC
            TempoNormal.Enabled = true;
            TempoNormal.Interval = 4000;
            TempoNormal.Start();

            GrillaLimpia(DgvDatos);
            LlenarGrilla();
            FormatoGrilla(DgvDatos);

            this.Show();

        }

        private void Direcciones()
        {
            oDirecciones.IpPLC = ConfigurationManager.AppSettings["IpPCL"].ToString();
            oDirecciones.PortPLC = Convert.ToInt16(ConfigurationManager.AppSettings["PortPLC"].ToString());
            oDirecciones.RackPLC = Convert.ToInt16(ConfigurationManager.AppSettings["RackPLC"].ToString());
            oDirecciones.SlotPLC = Convert.ToInt16(ConfigurationManager.AppSettings["SlotPLC"].ToString());
            oDirecciones.NumDb = Convert.ToInt16(ConfigurationManager.AppSettings["NumDb"].ToString());
            oDirecciones.PtoCOM = ConfigurationManager.AppSettings["PtoCOM"].ToString();
            COM = oDirecciones.PtoCOM;
        }

        private void FechaHoraServer()
        {
            try
            {
                ToolFechaHora.Text = lServer.RecuperaFechaServidor();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message.ToString(), "AVISO", MessageBoxButtons.OK, MessageBoxIcon.Error);
                RegErrores.RegistroLog(ex.Message.ToString());
            }

        }

        private void entradasToolStripMenuItem_Click(object sender, EventArgs e)
        {
            FrmLoguin frmloguin = new FrmLoguin();
            frmloguin.oSistemas.Cod_Sistema = 60;
            frmloguin.MdiParent = this;
            frmloguin.Show();
        }

        private void salidasToolStripMenuItem_Click(object sender, EventArgs e)
        {
            FrmLoguin frmloguin = new FrmLoguin();
            frmloguin.oSistemas.Cod_Sistema = 70;
            frmloguin.MdiParent = this;
            frmloguin.Show();
        }

        private void toolsMenu_Click(object sender, EventArgs e)
        {
            //if (EstadoTMP){OnStop();}

            //if (EstadoTMPHora) { OnStopHora(); }

            if (EstadoPLC) { oPLC.Close(); }

            this.Close();
        }

        private void pruebaDeCaídasToolStripMenuItem_Click(object sender, EventArgs e)
        {
            FrmLoguin frmloguin = new FrmLoguin();
            frmloguin.oSistemas.Cod_Sistema = 80;
            frmloguin.MdiParent = this;
            frmloguin.Show();
        }

        private void calibraciónToolStripMenuItem_Click(object sender, EventArgs e)
        {
            FrmLoguin frmloguin = new FrmLoguin();
            frmloguin.oSistemas.Cod_Sistema = 90;
            frmloguin.MdiParent = this;
            frmloguin.Show();
        }

        private void mantenedorUsuariosToolStripMenuItem_Click(object sender, EventArgs e)
        {
            FrmLoguin frmloguin = new FrmLoguin();
            frmloguin.oSistemas.Cod_Sistema = 1;
            frmloguin.MdiParent = this;
            frmloguin.Show();

        }

        private void mantenedorTiendasToolStripMenuItem_Click(object sender, EventArgs e)
        {
            FrmLoguin frmloguin = new FrmLoguin();
            frmloguin.oSistemas.Cod_Sistema = 2;
            frmloguin.MdiParent = this;
            frmloguin.Show();
        }

        private void mantenedorLineasToolStripMenuItem_Click(object sender, EventArgs e)
        {
            FrmLoguin frmloguin = new FrmLoguin();
            frmloguin.oSistemas.Cod_Sistema = 3;
            frmloguin.MdiParent = this;
            frmloguin.Show();
        }

        private void configuraciónSalidasTiendasToolStripMenuItem_Click(object sender, EventArgs e)
        {
            FrmLoguin frmloguin = new FrmLoguin();
            frmloguin.oSistemas.Cod_Sistema = 4;
            frmloguin.MdiParent = this;
            frmloguin.Show();
        }

        private void configuraciónOlasTiendasToolStripMenuItem_Click(object sender, EventArgs e)
        {
            FrmLoguin frmloguin = new FrmLoguin();
            frmloguin.oSistemas.Cod_Sistema = 5;
            frmloguin.MdiParent = this;
            frmloguin.Show();
        }

        #region JOB TEMPORIZADOR

        private void TempoNormal_Tick(object sender, EventArgs e)
        {
            Tarea();
        }

        private void Tarea()
        {
            TempoNormal.Stop();

            if (EstadoPLC)
            {
                //funcion lee estado start/stop
                LecturaEstadoPLC();

                PanelControl();

                //funcion lee emergencias
                LeerEmergencias();

                //funcion lee full line
                LeerSensorFullLine();
            }

            else
            {
                if (ping.PingServidores(oDirecciones.IpPLC))
                {
                    cConPLC();
                    this.ToolEstadoPlc.Text = "En Linea";
                    ToolEstadoPlc.ForeColor = Color.Green;
                }
                else
                {
                    this.ToolEstadoPlc.Text = "Desconectado";
                    ToolEstadoPlc.ForeColor = Color.Red;
                }
            }

            GrillaLimpia(DgvDatos);
            LlenarGrilla();
            FormatoGrilla(DgvDatos);

            TempoNormal.Start();
        }

        #endregion

        private void MDIFramework_FormClosed(object sender, FormClosedEventArgs e)
        {
            //if (EstadoTMP) { OnStop(); }

            //if (EstadoTMPHora) { OnStopHora(); }

            if (EstadoPLC) { oPLC.Close(); }
        }

        private void BtnStart_Click(object sender, EventArgs e)
        {

            if (MessageBox.Show("Desea encender sorter eTrolley?", "AVISO", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
            {
                try
                {
                    //oPLC.Write("DB3.DBX0.4", true);
                    //oPLC.Write("DB3.DBX0.4", false);

                    oPLC.Write("DB101.DBX0.3", true);
                    oPLC.Write("DB101.DBX0.3", false);
                    MessageBox.Show("sorter eTrolley se encuentra funcionando", "Advertencia", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message.ToString(), "Advertencia", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        private void BtnStop_Click(object sender, EventArgs e)
        {

            if (MessageBox.Show("Desea detener sorter eTrolley?", "AVISO", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
            {
                try
                {
                    oPLC.Write("DB101.DBX0.4", true);
                    oPLC.Write("DB101.DBX0.4", false);
                    MessageBox.Show("sorter eTrolley se encuentra detenido", "Advertencia", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message.ToString(), "Advertencia", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        private void reporteScannerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            FrmLoguin frmloguin = new FrmLoguin();
            frmloguin.oSistemas.Cod_Sistema = 6;
            frmloguin.MdiParent = this;
            frmloguin.Show();
        }

        private void recuperarContraseñaToolStripMenuItem_Click(object sender, EventArgs e)
        {
            FrmRecuperarPass frmRecupera = new FrmRecuperarPass();
            frmRecupera.ShowDialog();
        }

        #region CONTROLES
        private void PanelControl()
        {

            if (oTagPLC.bool160)
            {
                PbStart.Visible = true;
                PbStop.Visible = false;

                BtnStop.Visible = true;
                BtnStart.Visible = false;

                LblEstado.Visible = true;
                LblEstado.Text = "Funcionando";
                LblEstado.ForeColor = Color.Green;
            }

            else
            {
                PbStart.Visible = false;
                PbStop.Visible = true;

                LblEstado.Visible = true;
                LblEstado.Text = "Detenido";
                LblEstado.ForeColor = Color.Red;

                BtnStop.Visible = false;
                BtnStart.Visible = true;
            }
        }

        private void LeerEmergencias()
        {
            try
            {
                oTagPLC.Emergencia1 = (bool)oPLC.Read("DB101.DBX0.5");
                oTagPLC.Emergencia2 = (bool)oPLC.Read("DB101.DBX0.6");
                //oTagPLC.Emergencia3 = (bool)oPLC.Read("DB8.DBX1.1");

                if (oTagPLC.Emergencia1)
                {
                    Emergencia1.Visible = false;
                    LblEmer1.Visible = false;
                    //LblEmer1.Text = "Emergencia 1 Encendida";
                }
                else
                {
                    Emergencia1.Visible = true;
                    LblEmer1.Visible = true;
                    LblEmer1.Text = "Emergencia Panel Encendida";
                }

                if (oTagPLC.Emergencia2)
                {
                    Emergencia2.Visible = false;
                    LblEmer2.Visible = false;
                    //LblEmer2.Text = "Emergencia 2 Encendida";
                }
                else
                {
                    Emergencia2.Visible = true;
                    LblEmer2.Visible = true;
                    LblEmer2.Text = "Emergencia Botoneras Encendida";
                }
            }
            catch (Exception err) 
            {
 
            
            }

        }

        private void LecturaEstadoPLC()
        {
            try
            {
                //indica estado PLC
                //1 CORRIENDO
                //0 DETENIDO
                oTagPLC.bool160 = (bool)oPLC.Read("DB101.DBX0.0");
            }
            catch (Exception ex)
            {
                RegErrores.RegistroLog(ex.Message.ToString() + " funcion LecturaEstadoPLC");
            }


        }

        private void LeerSensorFullLine()
        {
            try
            {
                oTagPLC.FullLine1 = (bool)oPLC.Read("DB101.DBX111.4");
                oTagPLC.FullLine2 = (bool)oPLC.Read("DB101.DBX111.2");
                oTagPLC.FullLine3 = (bool)oPLC.Read("DB101.DBX111.3");

                if (oTagPLC.FullLine1)
                {
                    FullLine1.Visible = true;
                    FullLine1Est.Visible = true;
                    FullLine1Est.Text = "Linea Llena Recirculación";
                }
                else
                {
                    FullLine1.Visible = false;
                    FullLine1Est.Visible = false;
                    //FullLine1Est.Text = "Emergencia Panel Encendida";
                }

                if (oTagPLC.FullLine2)
                {
                    FullLine2.Visible = true;
                    FullLine2Est.Visible = true;
                    FullLine2Est.Text = "Linea Llena Trolley Vacío 1";
                }
                else
                {
                    FullLine2.Visible = false;
                    FullLine2Est.Visible = false;
                    //LblEmer2.Text = "Emergencia Botoneras Encendida";
                }

                if (oTagPLC.FullLine3)
                {
                    FullLine3.Visible = true;
                    FullLine3Est.Visible = true;
                    FullLine3Est.Text = "Linea Llena Trolley Vacío 2";
                }
                else
                {
                    FullLine3.Visible = false;
                    FullLine3Est.Visible = false;
                    //FullLine3Est.Text = "Emergencia 3 Encendida";
                }
            }

            catch (Exception err) 
            {
                            
            }

        }

        #endregion

        private void LlenarGrilla()
        {

            DataSet dsdatos = new DataSet();
            //lleno dataset
            dsdatos = _LGN_Insertar_Lecturas.Listado_CartonesLeidos();

            try
            {
                if (dsdatos != null)
                {
                    //abrir formulario de Carga
                    //FrmEspere.AbrirVentanaCarga(FrmEspere);

                    //Elimina el enlace de datos para poder limpiar
                    this.DgvDatos.DataSource = null;

                    //limpia los datos
                    this.DgvDatos.Rows.Clear();
                    this.DgvDatos.Columns.Clear();

                    //asigna la informacion a la grilla                    
                    this.DgvDatos.DataSource = dsdatos.Tables[0];

                }
                else
                {
                    //cierra formulario de Carga
                    //FrmEspere.CerrarVentanaCarga(FrmEspere);  

                }
            }
            catch (Exception ex)
            {
                //FrmEspere.CerrarVentanaCarga(FrmEspere);
            }

        }

        private void DgvDatos_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {

        }

        private void mantenedorDTDToolStripMenuItem_Click(object sender, EventArgs e)
        {
            FrmLoguin frmloguin = new FrmLoguin();
            frmloguin.oSistemas.Cod_Sistema = 8;
            frmloguin.MdiParent = this;
            frmloguin.Show();
        }

        private void button1_Click(object sender, EventArgs e)
        {
        }
    }
}
