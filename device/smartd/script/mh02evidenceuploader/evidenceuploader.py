import os
import shutil
import requests
import time
import json
from datetime import datetime, timedelta
import itertools

from collections import deque
from threading import Thread, Lock

with open('D:\\smartd\\script\\mh02model\\files\\configs\\smartd.json', 'r') as s:
    deviceconfig = json.load(s)

DEVICE_ID = deviceconfig["DEVICE_ID"]
SERVERIP = "10.2.187.218"
SERVERPORT = "9000" # sesuaikan berdasarkan list 
RECORDING_FOLDER = "D:\\pama\\ftp\\recording"
UPLOADED_FOLDER = "D:\\smartd\\script\\mh02evidenceuploader\\recording_uploaded"
WAITING_FOLDER = "D:\\smartd\\script\\mh02evidenceuploader\\recording_waiting"
RESERVED_FOLDER = "D:\\smartd\\script\\mh02evidenceuploader\\recording_reserved"
INVALID_FOLDER = "D:\\smartd\\script\\mh02evidenceuploader\\recording_invalid"

SMARTD_UPLOADER_STATUS = "D:\\pama\\ftp\\smartd_status_uploader.json"
 
SERVER_URL = f'http://{SERVERIP}:{SERVERPORT}/files'
RESET_FLAG = "D:\\smartd\\script\\mh02evidenceuploader\\evidenceuploadstop.id"
START_FLAG = "D:\\smartd\\script\\mh02evidenceuploader\\evidenceuploadstart.id"

stop_flag_lock = Lock()
stop_flag = False
worker1_deque = deque()
worker2_deque = deque()
worker_reserved_deque = deque()

def getAlerts(dirpath, filetype):
    i_dirs = os.listdir(dirpath)
    i_valid_dirs = []
    for item in i_dirs:
        if filetype == "DIR":
            if os.path.isdir(dirpath + "\\" + item):
                if (len(item.split("_")) > 2) and not "TESTING" in item:
                    i_valid_dirs.append(item)
        elif filetype == "FILE":
            # If filetype is FILE, remove file extension
            if os.path.isfile(dirpath + "\\" + item):
                if (len(item.split("_")) > 2) and not "TESTING" in item:
                    i_valid_dirs.append(item.split(".")[0])

    return i_valid_dirs


def init_uploader_status():
    uploader_status = {
        "last_update": datetime.now().isoformat(),
        "status": "OFFLINE"
    }
    with open(SMARTD_UPLOADER_STATUS, 'w') as f:
        json.dump(uploader_status, f)

def checkTimeLimit(_item):
    _now = datetime.now()
    _minus_one = _now + timedelta(minutes = -2)
    _minus_one_str = _minus_one.strftime("%Y%m%d%H%M%S")

    _item_arr = _item.split("_")
    _item_time_str = str(_item_arr[1]) + str(_item_arr[2])

    return (_item_time_str < _minus_one_str)


def upload_worker(p_worker_deque, worker_id):

    while True:
        # Check flag is exists
        with stop_flag_lock:
            if stop_flag:
                print(f"Worker {worker_id} stopping...")
                break
                
        # Check is not empty
        if len(p_worker_deque) < 1:
            time.sleep(1) 
            continue
            
        item = p_worker_deque.popleft()

        print(f"Worker {worker_id} uploading {len(item)} files in {len(p_worker_deque)}: ", item)

        if not checkTimeLimit(item):
            print("Skip item", item, " because fewer than time limit.")
            continue

        if not os.path.exists(WAITING_FOLDER + "\\" + item + ".zip"):
            continue

        try:
            with open(WAITING_FOLDER + "\\" + item + ".zip", 'rb') as zipf:
                
                if item in p_invalid_files:
                    _file_status = "INVALID"
                else:
                    _file_status = "VALID"

                zipfile = {'file': zipf}
                resp = requests.post(url=SERVER_URL + "?deviceid=" + DEVICE_ID + "&filestatus=" + _file_status, files=zipfile, timeout=90)

            if resp.text == "OK":
                # 4.d Move succeeded zip to uploaded folder
                print("Upload evidence:", item, " is success.")

                try:
                    if item in p_reserved_files:
                        os.remove(RESERVED_FOLDER + "\\" + item + ".txt")

                    if item in p_invalid_files:
                        shutil.move(INVALID_FOLDER + "\\" + item + ".zip", UPLOADED_FOLDER + "\\" + item + ".zip")
                    else:
                        shutil.move(WAITING_FOLDER + "\\" + item + ".zip", UPLOADED_FOLDER + "\\" + item + ".zip")
                except Exception as e:
                    print("[ERROR] Exception Move Files:", e)

            elif resp.text == "CORRUPT":

                try:
                    if item in p_invalid_files:
                        os.remove(INVALID_FOLDER + "\\" + item + ".zip")
                    else:
                        os.remove(WAITING_FOLDER + "\\" + item + ".zip")
                except Exception as e:
                    print("[ERROR] Exception Move Files:", e)
                        
            else:
                print("Uploading evidence:", item, " is failed.")
                time.sleep(2)

        except Exception as ex:
            print("[ERROR] Exception Upload:", ex)

def Dispatcher():
    global p_waiting_tobe_uploaded, p_invalid_files, p_reserved_files, worker1_deque, worker2_deque, worker_reserved_deque

    p_waiting_tobe_uploaded = []
    p_invalid_files = []
    p_reserved_files = []

    while True:
        # Check flag if exist
        with stop_flag_lock:
            if stop_flag:
                break
        # Get files in folders
        p_waiting_tobe_uploaded = getAlerts(WAITING_FOLDER,"FILE")
        p_invalid_files = getAlerts(INVALID_FOLDER,"FILE")
        # p_reserved_files = getAlerts(RESERVED_FOLDER,"FILE")

        # Consolidate item in Folder Invalid
        for item in p_invalid_files:
            if item not in p_waiting_tobe_uploaded:
                p_waiting_tobe_uploaded.append(item)

        # Sorting Descending
        p_waiting_tobe_uploaded.sort(key=lambda x: (x.split("_")[1] + x.split("_")[2]), reverse=True)

        # Set workers and create cycle iterator
        workers = [worker_reserved_deque, worker1_deque, worker2_deque]
        worker_iter = itertools.cycle(workers)

        # Cek daftar item yang sudah ada di semua worker deque
        existing_items = set(worker_reserved_deque) | set(worker1_deque) | set(worker2_deque)

        # Append hanya jika item belum ada di worker deque mana pun
        for item in p_waiting_tobe_uploaded:
            if item not in existing_items:  # Cegah duplikasi
                target_worker = next(worker_iter)  # Pilih worker secara bergiliran
                target_worker.append(item)  # Tambahkan item ke worker yang dapat giliran

        print("\n=== Final Deque States ===")
        print("Worker Reserved Deque:", list(worker_reserved_deque))
        print("Worker 1 Deque:", list(worker1_deque))
        print("Worker 2 Deque:", list(worker2_deque))
        print("==========================\n")

        time.sleep(60)


def start_workers():
    global worker1_thread, worker2_thread, worker_reserved_thread, dispatcher_thread

    if not worker1_thread.is_alive():
        worker1_thread.start()
    if not worker2_thread.is_alive():
        worker2_thread.start()
    if not worker_reserved_thread.is_alive():
        worker_reserved_thread.start()
    if not dispatcher_thread.is_alive():
        dispatcher_thread.start()


# Main Process

worker1_thread = Thread(target=upload_worker, args=(worker1_deque, 1))
worker2_thread = Thread(target=upload_worker, args=(worker2_deque, 2))
worker_reserved_thread = Thread(target=upload_worker, args=(worker_reserved_deque, 3))
dispatcher_thread = Thread(target=Dispatcher)

if not os.path.exists(UPLOADED_FOLDER):
    os.mkdir(UPLOADED_FOLDER)

if not os.path.exists(WAITING_FOLDER):
    os.mkdir(WAITING_FOLDER)

if not os.path.exists(RESERVED_FOLDER):
    os.mkdir(RESERVED_FOLDER)

if not os.path.exists(INVALID_FOLDER):
    os.mkdir(INVALID_FOLDER)

start_workers()

while(True):

    # print("::::::::::::INFORMATION::::::::::::")
    init_uploader_status()

    print("*******************************************", datetime.now())
    print(f"Running ON {DEVICE_ID}, {SERVERIP}, {SERVERPORT}")

    with open(SMARTD_UPLOADER_STATUS, 'r') as f:
        uploader_status = json.load(f)

    last_update = datetime.fromisoformat(uploader_status["last_update"])

    if datetime.now() - last_update > timedelta(minutes=5):
        uploader_status["status"] = "OFFLINE"
    else:
        uploader_status["status"] = "ONLINE"

    with open(SMARTD_UPLOADER_STATUS, 'w') as f:
        json.dump(uploader_status, f)


    if os.path.exists(RESET_FLAG):

        os.remove(RESET_FLAG)

        with stop_flag_lock:
            stop_flag = True

        if worker1_thread.is_alive():
            worker1_thread.join()
        if worker2_thread.is_alive():
            worker2_thread.join()
        if worker_reserved_thread.is_alive():
            worker_reserved_thread.join()
        if dispatcher_thread.is_alive():
            dispatcher_thread.join()

        print("Service is stopping.....")

    if os.path.exists(START_FLAG):
        os.remove(START_FLAG)

        with stop_flag_lock:
            stop_flag = False

        print("Starting service...")
        start_workers()

    time.sleep(1)
