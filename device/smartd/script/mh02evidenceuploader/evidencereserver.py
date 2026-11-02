import requests
from datetime import datetime, timedelta
import json
import time
import os

with open('D:\\smartd\\script\\mh02model\\files\\configs\\smartd.json', 'r') as s:
    deviceconfig = json.load(s)

DEVICE_ID = deviceconfig["DEVICE_ID"]
SERVERIP = "10.2.187.242"
SERVERPORT = "9010"

RESERVED_FOLDER = "D:\\smartd\\script\\mh02evidenceuploader\\recording_reserved"
RESET_FLAG = "evidencereserverstop.id"

def request_reservedfiles(device_id, reqtime):
    _url = f"http://{SERVERIP}:{SERVERPORT}/reserved?deviceid={device_id}&requesttime={reqtime}"
    try:
        _response = requests.get(_url)
        _response.raise_for_status() 
        if _response.status_code == 200:
            print("File data received:", _response.json())
            _response_data = _response.json()

            if 'filename' in _response_data:
                _filename = _response_data['filename']
                _data = _response_data['data']
                _filepath = os.path.join(RESERVED_FOLDER, _filename)

                with open(_filepath, 'w') as j:
                    json.dump(_data, j, indent=4)
                print(f"Data written to {_filepath}")

        elif _response.status_code == 404:
            print("[ERROR] No file found.")

        else:
            print("[ERROR] Requests error.")

            time.sleep(2)
    except requests.exceptions.RequestException as e:
        print(f"[ERROR] Request failed: {e}")

while True:
    _sendrequests = False

    _currtime = datetime.now() - timedelta(hours=2)
    _currdate = _currtime.strftime("%Y-%m-%d")
    _currhour = _currtime.strftime("%H")

    _reqtime = _currdate + "_" + _currhour

    _reservedfiles = os.listdir(RESERVED_FOLDER)

    if not any(file.endswith(".json") for file in _reservedfiles):
        _sendrequests = True

    if _sendrequests:
        request_reservedfiles(DEVICE_ID, _reqtime)
        _sendrequests = False
    else:
        print("File data json found")

    if os.path.exists(RESET_FLAG):
        os.remove(RESET_FLAG)
        print("Service is stopping.....")
        time.sleep(2)
        print("Service is stopped.")
        break
    
    time.sleep(300)
