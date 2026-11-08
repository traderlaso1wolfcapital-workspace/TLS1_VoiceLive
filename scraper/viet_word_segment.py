"""
Tách từ tiếng Việt không dấu từ username viết liền.
VD: "banggioidadenvoitoi" → "bằng giỏi đã đến với tôi"
"""

import sys
import json

# Từ điển tiếng Việt không dấu phổ biến (đã loại bỏ dấu)
VIET_WORDS = set("""
em anh chi toi may no co khong duoc roi mot hai ba bon nam sau bay tam chin
muoi tram nghin trieu ty ngay xua den voi di la nhung cai nay kia do
nguoi ban be ae ace mn a e c j k w q gi
minh dang se da tung bi boi vi the nen ma neu thi
lam cho that su that la that vay that su tot dep xau dep trai xinh gai
dep gai xinh dep xinh xan hoc hoi lam viec choi game mua ban ban mua
tien bac vang bac dong do la crypto coin bitcoin btc eth sol
binance okx remitano p2p usdt busd usdc vnd vndc
cho den khi nao bao gio luc nao hom nay ngay mai hom qua tuan sau
thang sau nam sau tet le tet nguyen dan giang sinh noel
dau tu chung khoan forex vang lai suat lai ngan hang
nha dat chung cu can ho o to xe may xe dap
an uong nha hang quan an cafe tra sua bia ruou
bong da the thao the hinh gym fitness yoga chay bo
am nhac phim anh sach bao doc viet sach truyen tranh
ca si dien vien nguoi mau hoa hau hot girl hot boy
dien thoai may tinh laptop iphone samsung xiaomi oppo
facebook tiktok youtube instagram zalo telegram twitter
viet nam ha noi sai gon tp hcm da nang hai phong can tho
mien bac mien trung mien nam tay nguyen dong nam bo
gai xinh giau co ngheo kho vui ve buon ba hanh phuc
yeu thuong ghet bo qua thuong nho tiec nuoi hanh dien
dung sai that gia mao hieu biet ngu dot gioi gioi dang cap
tuyet voi dinh cao xuat sac than thanh thanh cong that bai
may man xui xeo rui ro co hoi thach thuc kho khan de dang
nhanh cham som muon nang nhe dai ngan cao thap
gia re dat mac khuyen mai sale giam gia freeship
mien phi tra phi phi ship hang hieu fake auth chinh hang
like share subscribes follow tim heart tym
binh luan comment noi dung content video hinh anh
dep qua hay qua hay that tuyet hay vl dinh qua troi
ngu luon funny hai hai huoc vui nhon cute de thuong
ngau nhien hoi gioi sieu cap pro player noob ga
don gian phuc tap kho hieu de hieu ro rang minh bach
cuoi cutoi cuoi khuc khich ha ha hihi hoho
buon cuoi tuc cuoi vui tinh nghich ngo nghich ngom
thang thua duoc mat an thua thang thang cuoc ca cuoc
an tien an li xi cat toc lam dep trang diem
du lich du lich kham pha trai nghiem thu gian
tho gian nha nghi khach san homestay resort
tam bien bien dao nui rung song suoi thac nuoc
an choi nhan nghi cuoi tuan cuoc song hanh phuc
thanh thoi an nhien tu tai tam an lac quan
tich cuc tieu cuc nang luong suc khoe benh tat
covid sars cum sot ho hen di ung viem xoang
da lieu tim mach than kinh xuong khop
luat su bac si ky su giao vien sinh vien hoc sinh
cong nhan nong dan doanh nhan tri thuc lao cong
cha me ba me ong ba co di chu bac anh chi em
trai gai nam nu trong ngoai sang toi hom nay ngay mai
""".split())


def segment(username: str) -> str:
    """
    Tách username không dấu thành các từ có nghĩa dùng greedy matching.
    """
    s = username.lower().strip()
    if not s:
        return username

    result = []
    i = 0
    while i < len(s):
        best = None
        best_len = 0
        
        # Tìm từ dài nhất khớp
        for j in range(len(s), i, -1):
            candidate = s[i:j]
            if candidate in VIET_WORDS:
                if len(candidate) > best_len:
                    best = candidate
                    best_len = len(candidate)
            # Cũng thử với các từ 2-3 ký tự phổ biến
            if len(candidate) <= 3 and candidate in VIET_WORDS:
                if len(candidate) > best_len:
                    best = candidate
                    best_len = len(candidate)

        if best and best_len >= 2:
            result.append(best)
            i += best_len
        else:
            # Không tìm thấy từ dài, thử lấy ký tự tiếp theo
            result.append(s[i])
            i += 1

    return ' '.join(result)


if __name__ == "__main__":
    if len(sys.argv) > 1:
        username = ' '.join(sys.argv[1:])
    else:
        username = sys.stdin.read().strip()
    print(segment(username))